using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoachHub.Application.Assessments;
using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.Media;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Auth;
using CoachHub.Domain.Media;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using CoachHub.Domain.Nutrition;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Me;
using CoachHub.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Tenancy;

/// <summary>
/// Media ownership (§52 secure file storage, §62): platform media for everyone signed in, a
/// coach's library for the coach and their clients, client media for the client and their coach.
/// Proven on SQL Server, where the visibility filter and the new foreign keys run.
/// </summary>
public sealed class MediaOwnershipTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    [Fact]
    public async Task Platform_media_is_visible_to_every_signed_in_role_but_not_anonymously()
    {
        var admin = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator);
        var media = await UploadPlatformImageAsync(admin.Http);
        var coach = await factory.CreateCoachClientAsync();
        var client = await CreateClientAsync(coach);
        var portal = factory.CreateClientAreaClient(client.CoachId, client.Id);

        Assert.Equal(HttpStatusCode.OK, (await coach.GetAsync(MediaUrl(media.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync(MediaUrl(media.Id))).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(MediaUrl(media.Id))).StatusCode);
    }

    [Fact]
    public async Task A_coachs_picture_is_visible_to_their_clients_and_hidden_from_other_coaches()
    {
        var coachIdA = await factory.CreateCoachAsync();
        var coachA = await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachIdA);
        var clientOfA = await CreateClientAsync(coachA.Http);
        var coachB = await factory.CreateCoachClientAsync();
        var clientOfB = await CreateClientAsync(coachB);
        var avatar = await UploadAvatarAsync(coachA.Http);

        Assert.Equal(HttpStatusCode.OK, (await coachA.Http.GetAsync(MediaUrl(avatar.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await factory.CreateClientAreaClient(coachIdA, clientOfA.Id).GetAsync(MediaUrl(avatar.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateAdminClient().GetAsync(MediaUrl(avatar.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync(MediaUrl(avatar.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClientAreaClient(clientOfB.CoachId, clientOfB.Id).GetAsync(MediaUrl(avatar.Id))).StatusCode);
    }

    [Fact]
    public async Task A_clients_picture_is_visible_to_the_client_and_their_coach_only()
    {
        var coachA = await factory.CreateCoachClientAsync();
        var clientX = await CreateClientAsync(coachA);
        var clientY = await CreateClientAsync(coachA);
        var coachB = await factory.CreateCoachClientAsync();
        var x = await factory.CreateSignedInAccountAsync(AuthRoles.Client, clientX.CoachId, clientX.Id);
        var avatar = await UploadAvatarAsync(x.Http);

        Assert.Equal(HttpStatusCode.OK, (await x.Http.GetAsync(MediaUrl(avatar.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coachA.GetAsync(MediaUrl(avatar.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await factory.CreateClientAreaClient(clientY.CoachId, clientY.Id).GetAsync(MediaUrl(avatar.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachB.GetAsync(MediaUrl(avatar.Id))).StatusCode);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<CoachHubDbContext>().Set<MediaAsset>()
            .SingleAsync(media => media.Id == avatar.Id);
        Assert.Equal((clientX.CoachId, clientX.Id, x.UserId), (stored.CoachId!.Value, stored.ClientId!.Value, stored.OwnerUserId!.Value));
    }

    [Fact]
    public async Task Catalog_entries_accept_only_platform_images()
    {
        var admin = await factory.CreateSignedInAccountAsync(AuthRoles.Administrator);
        var coach = await factory.CreateCoachClientAsync();
        var client = await CreateClientAsync(coach);
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Client, client.CoachId, client.Id);
        var clientPhoto = await UploadAvatarAsync(account.Http);
        var platformImage = await UploadPlatformImageAsync(admin.Http);
        var category = await (await admin.Http.PostAsJsonAsync("/api/v1/reference-data/food-categories",
            new BilingualReferenceInput("Media " + Guid.NewGuid().ToString("N"), null)))
            .Content.ReadFromJsonAsync<BilingualReferenceResponse>();

        var leaked = await admin.Http.PostAsJsonAsync("/api/v1/nutrition/foods",
            new FoodInput("Leak", null, category!.Id, FoodBaseUnit.Gram, 100, 1, 1, 1, clientPhoto.Id));
        var allowed = await admin.Http.PostAsJsonAsync("/api/v1/nutrition/foods",
            new FoodInput("Rice " + Guid.NewGuid().ToString("N")[..6], null, category.Id, FoodBaseUnit.Gram, 100, 1, 1, 1, platformImage.Id));

        Assert.Equal(HttpStatusCode.BadRequest, leaked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_client_removes_their_login_sessions_submissions_and_media()
    {
        var coach = await factory.CreateCoachClientAsync();
        var client = await CreateClientAsync(coach);
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Client, client.CoachId, client.Id);
        var avatar = await UploadAvatarAsync(account.Http);
        var form = await PublishPhotoFormAsync(coach);
        var upload = await UploadFormMediaAsync(account.Http);
        var submitted = await SubmitPhotoAsync(account.Http, form, upload.Id);
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        var deleted = await coach.DeleteAsync($"/api/v1/clients/{client.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
        Assert.False(await db.Set<User>().AnyAsync(user => user.Id == account.UserId));
        Assert.False(await db.Set<UserSession>().AnyAsync(session => session.UserId == account.UserId));
        Assert.False(await db.Set<MediaAsset>().AnyAsync(media => media.Id == avatar.Id || media.Id == upload.Id));
        Assert.False(await db.Set<FormSubmission>().AnyAsync(submission => submission.ClientId == client.Id));
    }

    [Fact]
    public async Task A_client_cannot_attach_another_clients_upload_to_a_submission()
    {
        var coach = await factory.CreateCoachClientAsync();
        var owner = await CreateClientAsync(coach);
        var other = await CreateClientAsync(coach);
        var form = await PublishPhotoFormAsync(coach);
        var ownerAccount = await factory.CreateSignedInAccountAsync(AuthRoles.Client, owner.CoachId, owner.Id);
        var otherAccount = await factory.CreateSignedInAccountAsync(AuthRoles.Client, other.CoachId, other.Id);
        var ownersUpload = await UploadFormMediaAsync(ownerAccount.Http);

        var borrowed = await SubmitPhotoAsync(otherAccount.Http, form, ownersUpload.Id);

        Assert.Equal(HttpStatusCode.BadRequest, borrowed.StatusCode);
    }

    [Fact]
    public async Task A_transferred_clients_media_moves_to_the_new_coach()
    {
        var coachA = await factory.CreateCoachClientAsync();
        var coachIdB = await factory.CreateCoachAsync();
        var coachB = factory.CreateCoachClient(coachIdB);
        var client = await CreateClientAsync(coachA);
        var account = await factory.CreateSignedInAccountAsync(AuthRoles.Client, client.CoachId, client.Id);
        var avatar = await UploadAvatarAsync(account.Http);

        var transfer = await factory.CreateAdminClient().PostAsJsonAsync(
            $"/api/v1/clients/{client.Id}/transfer", new ClientTransferInput(coachIdB));

        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await coachB.GetAsync(MediaUrl(avatar.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await coachA.GetAsync(MediaUrl(avatar.Id))).StatusCode);
    }

    [Fact]
    public async Task Deactivating_a_coach_ends_their_sessions()
    {
        var coachId = await factory.CreateCoachAsync();
        var coach = await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId);

        var deactivate = await factory.CreateAdminClient().PutAsJsonAsync(
            $"/api/v1/admin/coaches/{coachId}", new Application.Coaches.CoachUpdateInput("Deactivated coach", false));

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachHubDbContext>();
        var session = await db.Set<UserSession>().SingleAsync(item => item.UserId == coach.UserId);
        Assert.Equal(SessionEndReason.AccountDisabled, session.EndReason);
    }

    private static string MediaUrl(Guid id) => $"/api/v1/media/{id}";

    private sealed record PhotoForm(Guid DefinitionId, Guid QuestionId);

    private static async Task<PhotoForm> PublishPhotoFormAsync(HttpClient coach)
    {
        var form = await ReadAsync<FormVersionResponse>(await coach.PostAsJsonAsync("/api/v1/assessment-forms",
            new CreateFormInput("Photos " + Guid.NewGuid().ToString("N")[..6], AssessmentFormType.UpdateAssessment)));
        var question = await ReadAsync<QuestionResponse>(await coach.PostAsJsonAsync(
            $"/api/v1/assessment-forms/{form.DefinitionId}/questions",
            new QuestionInput(null, "Front photo", QuestionType.MediaUpload, true, 0, [])));
        (await coach.PostAsync($"/api/v1/assessment-forms/{form.DefinitionId}/publish", null)).EnsureSuccessStatusCode();
        return new PhotoForm(form.DefinitionId, question.Id);
    }

    /// <summary>A signed-in client uploads a photo for a form answer (D5).</summary>
    private static async Task<MediaMetadata> UploadFormMediaAsync(HttpClient client)
    {
        var file = new ByteArrayContent(MeEndpointTests.Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", "front.png" } };
        return await ReadAsync<MediaMetadata>(await client.PostAsync("/api/v1/me/forms/media", form));
    }

    private static Task<HttpResponseMessage> SubmitPhotoAsync(HttpClient client, PhotoForm form, Guid mediaId) =>
        client.PostAsJsonAsync($"/api/v1/me/forms/{form.DefinitionId}/submissions", new MyFormSubmissionInput(
            [new AnswerInput(form.QuestionId, JsonSerializer.SerializeToElement<object?>(null), mediaId)]));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<MediaMetadata> UploadAvatarAsync(HttpClient http)
    {
        var response = await MeEndpointTests.UploadAvatarAsync(http, MeEndpointTests.Png, "image/png");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MediaMetadata>())!;
    }

    private static async Task<MediaMetadata> UploadPlatformImageAsync(HttpClient admin)
    {
        var file = new ByteArrayContent(MeEndpointTests.Png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var response = await admin.PostAsync("/api/v1/media", new MultipartFormDataContent { { file, "file", "food.png" } });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MediaMetadata>())!;
    }

    private static async Task<ClientResponse> CreateClientAsync(HttpClient coach)
    {
        var response = await coach.PostAsJsonAsync("/api/v1/clients",
            new ClientCreateInput("Media client " + Guid.NewGuid().ToString("N")[..6], null, null, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }
}
