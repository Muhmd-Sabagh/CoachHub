using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CoachHub.Application.Auth;
using CoachHub.Application.Chat;
using CoachHub.Application.ClientFiles;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using CoachHub.Application.Media;
using CoachHub.Application.Notifications;
using CoachHub.Domain.Chat;
using CoachHub.Domain.Communications;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Me;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Chat;

/// <summary>
/// P14 on SQL Server: coach ↔ client chat (§41) with files, history paging, read status, live delivery over SignalR
/// and "new message" notifications; shared files (§43); isolation (§62) and the transfer archive (D7).
/// </summary>
public sealed class ChatWorkflowTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private const string MyMessages = "/api/v1/me/chat/messages";

    [Fact]
    public async Task Coach_and_client_chat_with_files_paging_read_status_and_live_delivery()
    {
        var s = await SetUpAsync();
        await using var hub = await ConnectAsync(s.CoachToken);
        var received = new TaskCompletionSource<ChatMessageResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notified = new TaskCompletionSource<InboxItem>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<ChatMessageResponse>(RealtimeEvents.ChatMessage, m => received.TrySetResult(m));
        hub.On<InboxItem>(RealtimeEvents.Notification, n => notified.TrySetResult(n));

        var mine = (await s.Portal.GetFromJsonAsync<MyChatResponse>("/api/v1/me/chat"))!;
        Assert.Null(mine.Current.ConversationId);
        Assert.Empty(mine.Archived);

        // A message needs text or a file; the client attaches their own upload.
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Portal.PostAsJsonAsync(MyMessages, new ChatSendInput("  "))).StatusCode);
        var photo = await UploadAsync(s.Portal, "/api/v1/me/chat/attachments", MeEndpointTests.Png, "image/png", "lunch.png");
        var first = await ReadAsync<ChatMessageResponse>(await s.Portal.PostAsJsonAsync(MyMessages, new ChatSendInput("Here is my lunch", [photo.Id])));
        Assert.Equal((ChatParticipant.Client, "lunch.png"), (first.Sender, Assert.Single(first.Attachments).FileName));

        // The coach receives it live, with a notification.
        Assert.Equal(first.Id, (await received.Task.WaitAsync(TimeSpan.FromSeconds(15))).Id);
        var live = await notified.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal((NotificationEvent.NewMessage, $"/messages?client={s.ClientId}"), (live.Event, live.Link));

        // The coach opens the file but cannot attach a file they did not upload.
        Assert.Equal(HttpStatusCode.OK, (await s.Coach.GetAsync($"/api/v1/media/{photo.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.PostAsJsonAsync($"{s.ChatPath}/messages", new ChatSendInput(null, [photo.Id]))).StatusCode);

        var thread = (await s.Coach.GetFromJsonAsync<ChatThreadResponse>(s.ChatPath))!;
        Assert.Equal((1, first.Id), (thread.UnreadCount, Assert.Single(thread.Messages.Items).Id));
        var unread = (await s.Coach.GetFromJsonAsync<PagedResult<ConversationSummary>>("/api/v1/conversations?unreadOnly=true&pageSize=100"))!;
        Assert.Contains(unread.Items, x => x.ClientId == s.ClientId && x.UnreadCount == 1 && x.LastMessagePreview == "Here is my lunch");

        // One "new message" notification per conversation while it is unread.
        await ReadAsync<ChatMessageResponse>(await s.Portal.PostAsJsonAsync(MyMessages, new ChatSendInput("And dinner")));
        var inbox = (await s.Coach.GetFromJsonAsync<PagedResult<InboxItem>>("/api/v1/inbox?unreadOnly=true"))!;
        var note = Assert.Single(inbox.Items, x => x.Event == NotificationEvent.NewMessage);

        // Read status: the coach reads the chat; the client sees it read.
        Assert.Equal(HttpStatusCode.NoContent, (await s.Coach.PostAsync($"{s.ChatPath}/read", null)).StatusCode);
        (await s.Coach.PostAsync($"/api/v1/inbox/{note.Id}/read", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, (await s.Coach.GetFromJsonAsync<UnreadCountResponse>("/api/v1/inbox/unread-count"))!.Count);
        Assert.All((await s.Portal.GetFromJsonAsync<MyChatResponse>("/api/v1/me/chat"))!.Current.Messages.Items, x => Assert.NotNull(x.ReadAt));
        await ReadAsync<ChatMessageResponse>(await s.Portal.PostAsJsonAsync(MyMessages, new ChatSendInput("Thanks")));
        Assert.Equal(1, (await s.Coach.GetFromJsonAsync<UnreadCountResponse>("/api/v1/inbox/unread-count"))!.Count);

        // History paging, newest first.
        for (var i = 1; i <= 3; i++)
            await ReadAsync<ChatMessageResponse>(await s.Coach.PostAsJsonAsync($"{s.ChatPath}/messages", new ChatSendInput($"Reply {i}")));
        var page = (await s.Portal.GetFromJsonAsync<MyChatResponse>("/api/v1/me/chat?pageSize=4"))!.Current.Messages;
        Assert.Equal((4, true, "Reply 3"), (page.Items.Count, page.HasMore, page.Items[0].Text));
        var before = Uri.EscapeDataString(page.Items[^1].SentAt.ToString("O"));
        var older = (await s.Portal.GetFromJsonAsync<ChatMessagesPage>($"{MyMessages}?before={before}&pageSize=4"))!;
        Assert.Equal((2, false, first.Id), (older.Items.Count, older.HasMore, older.Items[^1].Id));
        var clientInbox = (await s.Portal.GetFromJsonAsync<PagedResult<InboxItem>>("/api/v1/inbox"))!;
        Assert.Equal("/app/chat", Assert.Single(clientInbox.Items, x => x.Event == NotificationEvent.NewMessage).Link);

        // Administrators read but do not write.
        using var admin = factory.CreateAdminClient();
        Assert.Equal(6, (await admin.GetFromJsonAsync<ChatThreadResponse>($"{s.ChatPath}?pageSize=50"))!.Messages.Items.Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"{s.ChatPath}/messages", new ChatSendInput("Hi"))).StatusCode);
    }

    [Fact]
    public async Task Chats_are_isolated_and_archived_for_the_client_on_transfer()
    {
        var s = await SetUpAsync();
        await ReadAsync<ChatMessageResponse>(await s.Portal.PostAsJsonAsync(MyMessages, new ChatSendInput("Hello coach")));
        await ReadAsync<ChatMessageResponse>(await s.Coach.PostAsJsonAsync($"{s.ChatPath}/messages", new ChatSendInput("Hi there")));
        var other = factory.CreateCoachClient(s.OtherCoachId);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(s.ChatPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"{s.ChatPath}/read", new { })).StatusCode);
        Assert.DoesNotContain((await other.GetFromJsonAsync<PagedResult<ConversationSummary>>("/api/v1/conversations?pageSize=100"))!.Items,
            x => x.ClientId == s.ClientId);
        var sibling = await ReadAsync<ClientResponse>(await s.Coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Sibling " + s.Tag, null, null, null)));
        var siblingPortal = factory.CreateClientAreaClient(s.CoachId, sibling.Id);
        Assert.Null((await siblingPortal.GetFromJsonAsync<MyChatResponse>("/api/v1/me/chat"))!.Current.ConversationId);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Portal.GetAsync("/api/v1/conversations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Portal.GetAsync(s.ChatPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Coach.GetAsync("/api/v1/me/chat")).StatusCode);

        // Transfer: the conversation is archived for the client; the new coach starts empty and never sees it.
        using var admin = factory.CreateAdminClient();
        (await admin.PostAsJsonAsync($"/api/v1/clients/{s.ClientId}/transfer", new ClientTransferInput(s.OtherCoachId))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await s.Coach.GetAsync(s.ChatPath)).StatusCode);
        var fresh = (await other.GetFromJsonAsync<ChatThreadResponse>(s.ChatPath))!;
        Assert.Equal((null, 0), (fresh.ConversationId, fresh.Messages.Items.Count));

        var portal = factory.CreateClientAreaClient(s.OtherCoachId, s.ClientId);
        var mine = (await portal.GetFromJsonAsync<MyChatResponse>("/api/v1/me/chat"))!;
        var archived = Assert.Single(mine.Archived);
        Assert.Equal(("Coach " + s.Tag, (Guid?)null), (archived.CoachName, mine.Current.ConversationId));
        var history = (await portal.GetFromJsonAsync<ChatMessagesPage>($"{MyMessages}?conversationId={archived.Id}"))!;
        Assert.Equal(["Hi there", "Hello coach"], history.Items.Select(x => x.Text));

        await ReadAsync<ChatMessageResponse>(await portal.PostAsJsonAsync(MyMessages, new ChatSendInput("Hello new coach")));
        Assert.Equal("Hello new coach", Assert.Single((await other.GetFromJsonAsync<ChatThreadResponse>(s.ChatPath))!.Messages.Items).Text);
        mine = (await portal.GetFromJsonAsync<MyChatResponse>("/api/v1/me/chat"))!;
        Assert.NotNull(mine.Current.ConversationId);
        Assert.Equal(archived.Id, Assert.Single(mine.Archived).Id);

        // Deleting the client removes its chats.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/clients/{s.ClientId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(s.ChatPath)).StatusCode);
    }

    [Fact]
    public async Task Coaches_share_files_the_client_opens()
    {
        var s = await SetUpAsync();
        var path = $"/api/v1/clients/{s.ClientId}/files";
        byte[] pdf = [.. "%PDF-1.4\n"u8, .. new byte[64]];
        byte[] mp4 = [0, 0, 0, 0x18, .. "ftypisom"u8, .. new byte[64]];
        byte[] docx = [0x50, 0x4B, 0x03, 0x04, .. new byte[64]];

        var guide = await ShareAsync(s.Coach, path, pdf, "application/pdf", "guide.pdf", "Meal guide");
        var video = await ShareAsync(s.Coach, path, mp4, "video/mp4", "squat.mp4", null);
        await ShareAsync(s.Coach, path, docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "plan.docx", "Plan");
        Assert.Equal(("Meal guide", "squat"), (guide.Title, video.Title));
        Assert.Equal(HttpStatusCode.BadRequest, (await PostFileAsync(s.Coach, path, [0x4D, 0x5A, 0, 0], "application/x-msdownload", "tool.exe", "Tool")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostFileAsync(s.Coach, path, MeEndpointTests.Png, "video/mp4", "fake.mp4", "Fake")).StatusCode);
        // Shared types stay limited to shared files and chat: other uploads keep images and PDF.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostFileAsync(s.Portal, "/api/v1/me/forms/media", mp4, "video/mp4", "squat.mp4", null)).StatusCode);

        var mine = (await s.Portal.GetFromJsonAsync<ClientFileResponse[]>("/api/v1/me/files"))!;
        Assert.Equal(3, mine.Length);
        Assert.Contains(mine, x => x.MediaId == video.MediaId && x.ContentType == "video/mp4" && x.FileName == "squat.mp4");

        // Videos open with range requests.
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/media/{video.MediaId}");
        request.Headers.Range = new RangeHeaderValue(0, 3);
        var partial = await s.Portal.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(4, (await partial.Content.ReadAsByteArrayAsync()).Length);

        // Isolation.
        var other = factory.CreateCoachClient(s.OtherCoachId);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"{path}/{guide.Id}")).StatusCode);
        var sibling = await ReadAsync<ClientResponse>(await s.Coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Sibling " + s.Tag, null, null, null)));
        var siblingPortal = factory.CreateClientAreaClient(s.CoachId, sibling.Id);
        Assert.Empty((await siblingPortal.GetFromJsonAsync<ClientFileResponse[]>("/api/v1/me/files"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await siblingPortal.GetAsync($"/api/v1/media/{guide.MediaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Portal.GetAsync(path)).StatusCode);

        // Rename and stop sharing.
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.PutAsJsonAsync($"{path}/{guide.Id}", new ClientFileInput(" "))).StatusCode);
        var renamed = await ReadAsync<ClientFileResponse>(await s.Coach.PutAsJsonAsync($"{path}/{guide.Id}", new ClientFileInput("Meal guide v2", "Week 3")));
        Assert.Equal(("Meal guide v2", "Week 3"), (renamed.Title, renamed.Description));
        Assert.Equal(HttpStatusCode.NoContent, (await s.Coach.DeleteAsync($"{path}/{guide.Id}")).StatusCode);
        Assert.Equal(2, (await s.Portal.GetFromJsonAsync<ClientFileResponse[]>("/api/v1/me/files"))!.Length);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Portal.GetAsync($"/api/v1/media/{guide.MediaId}")).StatusCode);

        // Deleting the client removes its shared files with their media.
        using var admin = factory.CreateAdminClient();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/clients/{s.ClientId}")).StatusCode);
    }

    private sealed record Setup(Guid CoachId, HttpClient Coach, string CoachToken, Guid OtherCoachId, Guid ClientId, HttpClient Portal, string Tag)
    {
        public string ChatPath => $"/api/v1/clients/{ClientId}/chat";
    }

    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync("Coach " + tag);
        var coach = await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId);
        var otherCoachId = await factory.CreateCoachAsync();
        var client = await ReadAsync<ClientResponse>(await coach.Http.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Chatter " + tag, null, null, null)));
        var portal = await factory.CreateSignedInAccountAsync(AuthRoles.Client, coachId, client.Id);
        return new Setup(coachId, coach.Http, coach.Http.DefaultRequestHeaders.Authorization!.Parameter!, otherCoachId, client.Id, portal.Http, tag);
    }

    private async Task<HubConnection> ConnectAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "api/v1/realtime"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.Transports = HttpTransportType.LongPolling;
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private static async Task<ClientFileResponse> ShareAsync(HttpClient http, string path, byte[] bytes, string type, string name, string? title) =>
        await ReadAsync<ClientFileResponse>(await PostFileAsync(http, path, bytes, type, name, title));

    private static Task<HttpResponseMessage> PostFileAsync(HttpClient http, string path, byte[] bytes, string type, string name, string? title)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        var form = new MultipartFormDataContent { { file, "file", name } };
        if (title is not null) form.Add(new StringContent(title), "title");
        return http.PostAsync(path, form);
    }

    private static async Task<MediaMetadata> UploadAsync(HttpClient http, string path, byte[] bytes, string type, string name) =>
        await ReadAsync<MediaMetadata>(await PostFileAsync(http, path, bytes, type, name, null));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
