using System.Net;
using System.Net.Http.Json;
using CoachHub.Application.Assessments;
using CoachHub.Application.Auth;
using CoachHub.Application.Billing;
using CoachHub.Application.Calendar;
using CoachHub.Application.CheckIns;
using CoachHub.Application.Clients;
using CoachHub.Application.Dashboards;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Domain.Assessments;
using CoachHub.Domain.Calendar;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Common;
using CoachHub.IntegrationTests.Auth;
using CoachHub.IntegrationTests.Persistence;
using CoachHub.IntegrationTests.WorkoutPlanning;

namespace CoachHub.IntegrationTests.Calendar;

/// <summary>
/// P15 on SQL Server: the client calendar (§45) with the rotation on the coach's training weekdays, the coach
/// calendar (§46), events (coach only), the coach dashboard (§38), the admin dashboard (§48) and revenue (D6), with
/// isolation (§62) and transfer (D7).
/// </summary>
public sealed class CalendarDashboardTests(CoachHubSqlApiFactory factory) : IClassFixture<CoachHubSqlApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Monday = Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));

    [Fact]
    public async Task Client_calendar_places_the_rotation_on_training_days_with_check_ins_programs_and_events()
    {
        var s = await SetUpAsync();
        var week = $"from={Monday:yyyy-MM-dd}&to={Monday.AddDays(6):yyyy-MM-dd}";

        // No training days yet: no workout or rest days.
        var calendar = (await s.Coach.GetFromJsonAsync<ClientCalendarResponse>($"{s.CalendarPath}?{week}"))!;
        Assert.Empty(calendar.TrainingWeekdays);
        Assert.DoesNotContain(calendar.Entries, x => x.Kind is CalendarEntryKind.WorkoutDay or CalendarEntryKind.RestDay);

        // Mon/Wed/Fri: Push, Pull, Legs; the other days rest.
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.PutAsJsonAsync($"{s.CalendarPath}/training-days", new TrainingDaysInput([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.PutAsJsonAsync($"{s.CalendarPath}/training-days", new { weekdays = new[] { "Funday" } })).StatusCode);
        await ReadAsync<ClientCalendarResponse>(await s.Coach.PutAsJsonAsync($"{s.CalendarPath}/training-days",
            new TrainingDaysInput([DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Wednesday])));
        calendar = (await s.Portal.GetFromJsonAsync<ClientCalendarResponse>($"/api/v1/me/calendar?{week}"))!;
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], calendar.TrainingWeekdays);
        var days = calendar.Entries.Where(x => x.Kind is CalendarEntryKind.WorkoutDay or CalendarEntryKind.RestDay)
            .ToDictionary(x => x.Date, x => x.Kind == CalendarEntryKind.RestDay ? "Rest" : x.Title);
        Assert.Equal(["Push", "Rest", "Pull", "Rest", "Legs", "Rest", "Rest"], Enumerable.Range(0, 7).Select(i => days[Monday.AddDays(i)]));

        // A logged workout shows as done on its day.
        var session = await ReadAsync<WorkoutSessionResponse>(await s.Portal.PostAsJsonAsync("/api/v1/me/training/sessions",
            new StartWorkoutInput(Today, NameEn: "Extra session")));
        session = await ReadAsync<WorkoutSessionResponse>(await s.Portal.PostAsJsonAsync($"/api/v1/me/training/sessions/{session.Id}/exercises",
            new AddLoggedExerciseInput(s.ExerciseId)));
        (await s.Portal.PostAsJsonAsync($"/api/v1/me/training/sessions/{session.Id}/exercises/{session.Exercises[0].Id}/sets",
            new LoggedSetInput(Reps: 5, WeightKg: 60))).EnsureSuccessStatusCode();
        (await s.Portal.PostAsync($"/api/v1/me/training/sessions/{session.Id}/complete", null)).EnsureSuccessStatusCode();
        calendar = (await s.Portal.GetFromJsonAsync<ClientCalendarResponse>($"/api/v1/me/calendar?{week}"))!;
        var done = Assert.Single(calendar.Entries, x => x.ReferenceId == session.Id);
        Assert.Equal((CalendarEntryKind.WorkoutDay, true, "Extra session"), (done.Kind, done.Done, done.Title));

        // Check-ins due, and the plan's start and end dates.
        await ScheduleCheckInAsync(s);
        await ReadAsync<WorkoutPlanResponse>(await s.Coach.PutAsJsonAsync($"/api/v1/workout-plans/{s.PlanId}/revisions/current",
            s.PlanInput with { BaseVersionNumber = 1, StartDate = Monday, EndDate = Monday.AddDays(27) }));
        calendar = (await s.Portal.GetFromJsonAsync<ClientCalendarResponse>($"/api/v1/me/calendar?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(27):yyyy-MM-dd}"))!;
        Assert.Equal(4, calendar.Entries.Count(x => x.Kind == CalendarEntryKind.CheckInDue));
        Assert.Single(calendar.Entries, x => x.Kind == CalendarEntryKind.ProgramStart && x.Date == Monday && x.PlanKind == PlanKind.Workout);
        Assert.Single(calendar.Entries, x => x.Kind == CalendarEntryKind.ProgramEnd && x.Date == Monday.AddDays(27));

        // Coaches add events; the client sees the shared ones only and cannot add any.
        var shared = await ReadAsync<CalendarEventResponse>(await s.Coach.PostAsJsonAsync("/api/v1/calendar/events",
            new CalendarEventInput(s.ClientId, CalendarEventKind.Event, "Competition", Today.AddDays(3), "Bring your belt", new TimeOnly(9, 0), new TimeOnly(12, 0), true)));
        var followUp = await ReadAsync<CalendarEventResponse>(await s.Coach.PostAsJsonAsync("/api/v1/calendar/events",
            new CalendarEventInput(s.ClientId, CalendarEventKind.FollowUp, "Call about the knee", Today.AddDays(1))));
        var own = await ReadAsync<CalendarEventResponse>(await s.Coach.PostAsJsonAsync("/api/v1/calendar/events",
            new CalendarEventInput(null, CalendarEventKind.Event, "Day off", Today.AddDays(2))));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.PostAsJsonAsync("/api/v1/calendar/events",
            new CalendarEventInput(s.ClientId, CalendarEventKind.FollowUp, "Shown?", Today, VisibleToClient: true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Portal.PostAsJsonAsync("/api/v1/calendar/events",
            new CalendarEventInput(s.ClientId, CalendarEventKind.Event, "Mine", Today))).StatusCode);
        var range = $"from={Today:yyyy-MM-dd}&to={Today.AddDays(6):yyyy-MM-dd}";
        var mine = (await s.Portal.GetFromJsonAsync<ClientCalendarResponse>($"/api/v1/me/calendar?{range}"))!;
        Assert.Equal([shared.Id], mine.Entries.Where(x => x.Kind is CalendarEntryKind.Event or CalendarEntryKind.FollowUp).Select(x => x.ReferenceId));
        Assert.Equal((new TimeOnly(9, 0), "Bring your belt"), (mine.Entries.Single(x => x.ReferenceId == shared.Id).StartTime!.Value, mine.Entries.Single(x => x.ReferenceId == shared.Id).Detail));

        // The coach calendar shows every client's entries and the coach's own events.
        var coachCalendar = (await s.Coach.GetFromJsonAsync<CoachCalendarResponse>($"/api/v1/calendar?{range}"))!;
        Assert.Contains(coachCalendar.Entries, x => x.ReferenceId == followUp.Id && x.Kind == CalendarEntryKind.FollowUp && x.Done == false && x.ClientName == s.ClientName);
        Assert.Contains(coachCalendar.Entries, x => x.ReferenceId == own.Id && x.ClientId == null);
        Assert.Contains(coachCalendar.Entries, x => x.Kind == CalendarEntryKind.CheckInDue && x.ClientId == s.ClientId);
        var doneFollowUp = await ReadAsync<CalendarEventResponse>(await s.Coach.PutAsJsonAsync($"/api/v1/calendar/events/{followUp.Id}",
            new CalendarEventInput(s.ClientId, CalendarEventKind.FollowUp, "Call about the knee", Today.AddDays(1), IsDone: true)));
        Assert.True(doneFollowUp.IsDone);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.PutAsJsonAsync($"/api/v1/calendar/events/{followUp.Id}",
            new CalendarEventInput(null, CalendarEventKind.FollowUp, "Moved", Today))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.GetAsync($"/api/v1/calendar?from={Today:yyyy-MM-dd}&to={Today.AddDays(90):yyyy-MM-dd}")).StatusCode);

        // Isolation.
        Assert.Equal(HttpStatusCode.NotFound, (await s.OtherCoach.GetAsync($"{s.CalendarPath}?{range}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.OtherCoach.PutAsJsonAsync($"/api/v1/calendar/events/{shared.Id}",
            new CalendarEventInput(s.ClientId, CalendarEventKind.Event, "Hijack", Today, VisibleToClient: true))).StatusCode);
        Assert.DoesNotContain((await s.OtherCoach.GetFromJsonAsync<CoachCalendarResponse>($"/api/v1/calendar?{range}"))!.Entries, x => x.ClientId == s.ClientId);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Portal.GetAsync($"/api/v1/calendar?{range}")).StatusCode);

        // Transfer: the shared event and the training days move; the private follow-up stays with the old coach.
        using var admin = factory.CreateAdminClient();
        (await admin.PostAsJsonAsync($"/api/v1/clients/{s.ClientId}/transfer", new ClientTransferInput(s.OtherCoachId))).EnsureSuccessStatusCode();
        var moved = (await s.OtherCoach.GetFromJsonAsync<ClientCalendarResponse>($"{s.CalendarPath}?{range}"))!;
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], moved.TrainingWeekdays);
        Assert.Contains(moved.Entries, x => x.ReferenceId == shared.Id);
        Assert.DoesNotContain(moved.Entries, x => x.ReferenceId == followUp.Id);
        var kept = (await s.Coach.GetFromJsonAsync<CoachCalendarResponse>($"/api/v1/calendar?{range}"))!;
        Assert.Contains(kept.Entries, x => x.ReferenceId == followUp.Id && x.ClientId == null);
        Assert.DoesNotContain(kept.Entries, x => x.ReferenceId == shared.Id);
    }

    [Fact]
    public async Task Dashboards_report_the_coach_business_and_the_platform_with_revenue()
    {
        var s = await SetUpAsync();
        using var admin = factory.CreateAdminClient();
        await ScheduleCheckInAsync(s);
        // The plan ended last week: an expired program.
        await ReadAsync<WorkoutPlanResponse>(await s.Coach.PutAsJsonAsync($"/api/v1/workout-plans/{s.PlanId}/revisions/current",
            s.PlanInput with { BaseVersionNumber = 1, StartDate = Today.AddDays(-60), EndDate = Today.AddDays(-7) }));
        var inactive = await ReadAsync<ClientResponse>(await s.Coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput("Paused " + s.Tag, null, null, null)));
        (await s.Coach.PutAsJsonAsync($"/api/v1/clients/{inactive.Id}", new ClientUpdateInput(inactive.Name, null, null, false))).EnsureSuccessStatusCode();

        // Revenue: a payment of 900 and a refund of 100.
        var currency = await ReadAsync<CurrencyResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/currencies",
            new CurrencyInput("R" + s.Tag[..4].ToUpperInvariant(), "Revenue currency", "$", true)));
        var invoice = await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync("/api/v1/billing/invoices",
            new InvoiceInput(s.ClientId, null, currency.Id, 900, null)));
        invoice = await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/billing/invoices/{invoice.Id}/payments", new PaymentInput(null, 900, null)));
        await ReadAsync<InvoiceResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/billing/invoices/payments/{invoice.Payments[0].Id}/refunds", new RefundInput(100, "Goodwill")));

        var coach = (await s.Coach.GetFromJsonAsync<CoachDashboard>("/api/v1/dashboard/coach"))!;
        Assert.Equal((2, 1, 1), (coach.Clients.Total, coach.Clients.Active, coach.Clients.Inactive));
        Assert.Equal(1, coach.Statuses.Single(x => x.Status == ClientStatus.Inactive).Count);
        Assert.Equal(Enum.GetValues<ClientStatus>().Length, coach.Statuses.Count);
        Assert.Equal((0, 1, 0), (coach.CheckIns.AwaitingReview, coach.CheckIns.Due, coach.CheckIns.Overdue));
        var expired = Assert.Single(coach.ExpiredPrograms);
        Assert.Equal((s.ClientId, PlanKind.Workout, Today.AddDays(-7)), (expired.ClientId, expired.Kind, expired.EndedOn));
        Assert.NotEmpty(coach.RecentActivity);
        Assert.Contains(coach.RecentActivity, x => x.ClientId == s.ClientId && x.ClientName == s.ClientName);
        // P18: sign-ins and notifications stay in the audit trail but not in the dashboard's recent activity.
        Assert.DoesNotContain(coach.RecentActivity, x => x.EntityType is "UserSession" or "Notification");
        var revenue = Assert.Single(coach.Revenue);
        Assert.Equal((currency.Code, 900m, 100m, 800m), (revenue.CurrencyCode, revenue.Settled, revenue.Refunded, revenue.Net));
        Assert.Empty((await s.OtherCoach.GetFromJsonAsync<CoachDashboard>("/api/v1/dashboard/coach"))!.Revenue);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Coach.GetAsync($"/api/v1/dashboard/coach?from={Today:yyyy-MM-dd}&to={Today.AddDays(-1):yyyy-MM-dd}")).StatusCode);

        // Administrators see the platform, with revenue per currency and per coach.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Coach.GetAsync("/api/v1/dashboard/admin")).StatusCode);
        var platform = (await admin.GetFromJsonAsync<AdminDashboard>("/api/v1/dashboard/admin"))!;
        Assert.True(platform.Coaches >= 2 && platform.Clients >= 2 && platform.ActiveUsers >= 2);
        Assert.True(platform.NewUsers.Coaches >= 2);
        Assert.Equal(800m, Assert.Single(platform.RevenueByCurrency, x => x.CurrencyCode == currency.Code).Net);
        Assert.Equal(800m, Assert.Single(platform.RevenueByCoach, x => x.Id == s.CoachId).Net);
        Assert.Equal(30, platform.UserActivity.Count);
        Assert.True(platform.UserActivity[^1].ActiveUsers >= 1 && platform.UserActivity[^1].Changes > 0);
        Assert.True(platform.Usage.WorkoutsLogged >= 0 && platform.Usage.FormSubmissions >= 0);
    }

    private async Task ScheduleCheckInAsync(Setup s)
    {
        var form = await ReadAsync<FormVersionResponse>(await s.Coach.PostAsJsonAsync("/api/v1/assessment-forms",
            new CreateFormInput("Weekly " + s.Tag, AssessmentFormType.CheckIn)));
        await ReadAsync<QuestionResponse>(await s.Coach.PostAsJsonAsync($"/api/v1/assessment-forms/{form.DefinitionId}/questions",
            new QuestionInput(null, "Energy", QuestionType.Number, true, 0, [], null)));
        (await s.Coach.PostAsync($"/api/v1/assessment-forms/{form.DefinitionId}/publish", null)).EnsureSuccessStatusCode();
        (await s.Coach.PutAsJsonAsync($"/api/v1/clients/{s.ClientId}/check-in-schedule",
            new CheckInScheduleInput(form.DefinitionId, 1, Today.AddDays(2).DayOfWeek, Monday))).EnsureSuccessStatusCode();
    }

    private sealed record Setup(Guid CoachId, HttpClient Coach, Guid OtherCoachId, HttpClient OtherCoach, Guid ClientId, string ClientName,
        HttpClient Portal, Guid PlanId, WorkoutPlanInput PlanInput, Guid ExerciseId, string Tag)
    {
        public string CalendarPath => $"/api/v1/clients/{ClientId}/calendar";
    }

    private async Task<Setup> SetUpAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var coachId = await factory.CreateCoachAsync();
        var coach = (await factory.CreateSignedInAccountAsync(AuthRoles.Coach, coachId)).Http;
        var otherCoachId = await factory.CreateCoachAsync();
        using var admin = factory.CreateAdminClient();
        var category = (await ReadAsync<BilingualReferenceResponse>(await admin.PostAsJsonAsync("/api/v1/reference-data/exercise-categories",
            new BilingualReferenceInput("Calendar " + tag, null)))).Id;
        var squat = await ReadAsync<ExerciseResponse>(await admin.PostAsJsonAsync("/api/v1/training/exercises",
            new ExerciseInput($"Squat {tag}", null, category, null, null, PrimaryMuscleGroupId: TestWorkouts.Quadriceps)));
        var name = "Planner " + tag;
        var client = await ReadAsync<ClientResponse>(await coach.PostAsJsonAsync("/api/v1/clients", new ClientCreateInput(name, null, null, null)));
        var portal = (await factory.CreateSignedInAccountAsync(AuthRoles.Client, coachId, client.Id)).Http;
        WorkoutDayInput Day(string dayName, int order) =>
            new(Guid.NewGuid(), dayName, null, null, null, order, [new(Guid.NewGuid(), squat.Id, 0, null, TestWorkouts.Sets(3, 5, loadKg: 100))]);
        var input = new WorkoutPlanInput($"Rotation {tag}", null, client.Id, [], [Day("Push", 0), Day("Pull", 1), Day("Legs", 2)]);
        var plan = await ReadAsync<WorkoutPlanResponse>(await coach.PostAsJsonAsync("/api/v1/workout-plans", input));
        return new Setup(coachId, coach, otherCoachId, factory.CreateCoachClient(otherCoachId), client.Id, name, portal, plan.Id, input, squat.Id, tag);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
