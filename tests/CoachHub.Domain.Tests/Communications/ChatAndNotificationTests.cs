using CoachHub.Domain.Chat;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.ClientFiles;
using CoachHub.Domain.Common;
using CoachHub.Domain.Communications;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Domain.Tests.Communications;

public sealed class ChatAndNotificationTests
{
    private static readonly Guid Coach = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_message_has_text_or_files_and_raises_one_event()
    {
        var conversation = Conversation.Start(Coach, Client, Now);
        var message = ChatMessage.Send(conversation, ChatParticipant.Client, User, "  Hi coach  ", [], Now);
        Assert.Equal(("Hi coach", Coach, Client, conversation.Id), (message.Text, message.CoachId, message.ClientId, message.ConversationId));
        Assert.Equal(Now, conversation.LastMessageAt);
        var sent = Assert.IsType<ChatMessageSent>(Assert.Single(message.TakeDomainEvents()));
        Assert.Equal((message.Id, ChatParticipant.Client), (sent.MessageId, sent.Sender));
        Assert.Empty(message.TakeDomainEvents());

        var file = new ChatAttachmentInput(Guid.NewGuid(), " plan.pdf ", "application/pdf", 1200);
        var withFile = ChatMessage.Send(conversation, ChatParticipant.Coach, User, null, [file], Now);
        Assert.Equal(("plan.pdf", (string?)null), (Assert.Single(withFile.Attachments).FileName, withFile.Text));

        Assert.Equal("text", Assert.Throws<ArgumentException>(() => ChatMessage.Send(conversation, ChatParticipant.Client, User, " ", [], Now)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentException>(() =>
            ChatMessage.Send(conversation, ChatParticipant.Client, User, new string('x', ChatMessage.MaximumTextLength + 1), [], Now)).ParamName);
        Assert.Equal("attachments", Assert.Throws<ArgumentException>(() =>
            ChatMessage.Send(conversation, ChatParticipant.Client, User, null, [file, file], Now)).ParamName);
        Assert.Equal("attachments", Assert.Throws<ArgumentException>(() => ChatMessage.Send(conversation, ChatParticipant.Client, User, null,
            Enumerable.Range(0, 11).Select(_ => file with { MediaId = Guid.NewGuid() }).ToArray(), Now)).ParamName);
    }

    [Fact]
    public void An_archived_conversation_is_read_only_and_names_the_previous_coach()
    {
        var conversation = Conversation.Start(Coach, Client, Now);
        conversation.Archive("  Coach Sam ", Now.AddDays(1));
        conversation.Archive("Someone else", Now.AddDays(2));
        Assert.Equal((true, "Coach Sam", Now.AddDays(1)), (conversation.IsArchived, conversation.ArchivedCoachName, conversation.ArchivedAt!.Value));
        Assert.Throws<InvalidOperationException>(() => ChatMessage.Send(conversation, ChatParticipant.Client, User, "Hello?", [], Now));
    }

    [Fact]
    public void Reading_keeps_the_first_time()
    {
        var message = ChatMessage.Send(Conversation.Start(Coach, Client, Now), ChatParticipant.Coach, User, "Hi", [], Now);
        message.MarkRead(Now.AddMinutes(1));
        message.MarkRead(Now.AddMinutes(5));
        Assert.Equal(Now.AddMinutes(1), message.ReadAt);
    }

    [Fact]
    public void Event_notifications_are_in_app_or_email_for_one_account()
    {
        var inApp = Notification.ForEvent(Coach, Client, User, NotificationChannel.InApp, User.ToString(), NotificationEvent.NewDiet,
            "New diet plan", "Body", "/app/nutrition", "plan:1", Now);
        Assert.Equal((NotificationStatus.Sent, Now, NotificationEvent.NewDiet, "/app/nutrition"), (inApp.Status, inApp.SentAt!.Value, inApp.Event!.Value, inApp.Link));
        inApp.MarkRead(Now.AddMinutes(1));
        inApp.MarkRead(Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(1), inApp.ReadAt);

        var email = Notification.ForEvent(Coach, Client, User, NotificationChannel.Email, "c@x.test", NotificationEvent.NewDiet, "T", "B", "/app", null, Now);
        Assert.Equal(NotificationStatus.Pending, email.Status);
        Assert.Throws<InvalidOperationException>(() => email.MarkRead(Now));
        Assert.Throws<ArgumentException>(() => Notification.ForEvent(Coach, Client, User, NotificationChannel.WhatsApp, "1", NotificationEvent.NewDiet, "T", "B", "/app", null, Now));
        Assert.Throws<ArgumentException>(() => Notification.ForEvent(Coach, Client, User, NotificationChannel.InApp, "u", NotificationEvent.NewDiet, "T", "B", "https://evil.test", null, Now));
        Assert.Throws<ArgumentException>(() => Notification.Schedule(Coach, Client, NotificationChannel.InApp, "u", "T", "B", Now, false));
    }

    [Fact]
    public void Email_preferences_default_to_everything_but_messages()
    {
        Assert.True(NotificationPreference.WantsEmail(NotificationPreference.Defaults, NotificationEvent.CheckInRequired));
        Assert.False(NotificationPreference.WantsEmail(NotificationPreference.Defaults, NotificationEvent.NewMessage));
        var preference = NotificationPreference.Create(User, NotificationEmailEvents.NewMessage, Now);
        Assert.True(NotificationPreference.WantsEmail(preference.EmailEvents, NotificationEvent.NewMessage));
        Assert.False(NotificationPreference.WantsEmail(preference.EmailEvents, NotificationEvent.NewDiet));
        Assert.Equal(Enum.GetValues<NotificationEvent>().Length, Enum.GetValues<NotificationEvent>().Count(x => NotificationPreference.WantsEmail(NotificationEmailEvents.All, x)));
        Assert.Throws<ArgumentException>(() => preference.Change((NotificationEmailEvents)(1 << 20), Now));
    }

    [Fact]
    public void Plan_versions_raise_activation_and_update_events()
    {
        var planId = Guid.NewGuid();
        var draft = DietPlanRevision.Create(planId, 1, PlanRevisionStatus.Draft, null, null, User, Now);
        Assert.Empty(draft.TakeDomainEvents());
        draft.Touch(Now);
        Assert.Empty(draft.TakeDomainEvents());
        draft.Update(PlanRevisionStatus.Active, null, null, Now);
        Assert.Equal(new PlanVersionActivated(PlanKind.Diet, planId, draft.Id, 1), Assert.Single(draft.TakeDomainEvents()));
        draft.Update(PlanRevisionStatus.Active, null, null, Now);
        Assert.Equal(new ActivePlanVersionSaved(PlanKind.Diet, planId, draft.Id), Assert.Single(draft.TakeDomainEvents()));
        draft.Touch(Now);
        Assert.IsType<ActivePlanVersionSaved>(Assert.Single(draft.TakeDomainEvents()));

        var second = WorkoutPlanRevision.Create(planId, 2, PlanRevisionStatus.Active, null, null, User, Now);
        Assert.Equal(new PlanVersionActivated(PlanKind.Workout, planId, second.Id, 2), Assert.Single(second.TakeDomainEvents()));
    }

    [Fact]
    public void Giving_a_plan_to_a_client_raises_an_assignment()
    {
        var plan = WorkoutPlan.Create(Coach, "Push pull legs", null, null, Now);
        Assert.Empty(plan.TakeDomainEvents());
        plan.Assign(Client);
        Assert.Equal(new PlanAssigned(PlanKind.Workout, plan.Id, Client), Assert.Single(plan.TakeDomainEvents()));
        plan.Update("Push pull legs", null, Client);
        Assert.Empty(plan.TakeDomainEvents());
        var diet = DietPlan.Create(Coach, "Cut", null, Client, Now);
        Assert.IsType<PlanAssigned>(Assert.Single(diet.TakeDomainEvents()));
    }

    [Fact]
    public void A_shared_file_needs_a_title()
    {
        var file = ClientFile.Share(Coach, Client, Guid.NewGuid(), "  Meal guide ", " ", User, Now);
        Assert.Equal(("Meal guide", (string?)null), (file.Title, file.Description));
        Assert.Equal("title", Assert.Throws<ArgumentException>(() => file.Describe(" ", null)).ParamName);
        Assert.Equal("description", Assert.Throws<ArgumentException>(() =>
            file.Describe("Guide", new string('x', ClientFile.MaximumDescriptionLength + 1))).ParamName);
    }
}
