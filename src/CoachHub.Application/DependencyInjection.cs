using CoachHub.Application.Auditing;
using CoachHub.Application.Adherence;
using CoachHub.Application.Assessments;
using CoachHub.Application.Calendar;
using CoachHub.Application.Chat;
using CoachHub.Application.CheckIns;
using CoachHub.Application.ClientFiles;
using CoachHub.Application.Common.Events;
using CoachHub.Application.FitnessTests;
using CoachHub.Application.Assessments.Importing;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Billing;
using CoachHub.Application.Coaches;
using CoachHub.Application.Communications;
using CoachHub.Application.Clients;
using CoachHub.Application.Dashboards;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.FoodTracking;
using CoachHub.Application.Media;
using CoachHub.Application.Pdf;
using CoachHub.Application.PlanDelivery;
using CoachHub.Application.Notifications;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Progress;
using CoachHub.Application.Reporting;
using CoachHub.Application.SavedPlans;
using CoachHub.Application.Settings;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutTracking;
using CoachHub.Application.WorkoutPlanning;
using CoachHub.Domain.Chat;
using CoachHub.Domain.CheckIns;
using CoachHub.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuditQueryService>();
        services.AddScoped<CoachOwnership>();
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<SessionService>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddScoped<BrandingService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<BillingService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<PlanDeliveryService>();
        services.AddScoped<FormAdminService>();
        services.AddScoped<FormSubmissionService>();
        services.AddScoped<AssessmentAdminQueryService>();
        services.AddScoped<AssessmentImportService>();
        services.AddScoped<ClientService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<DietPlanService>();
        services.AddScoped<NutritionTargetService>();
        services.AddScoped<FoodTrackingService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MediaService>();
        services.AddScoped<PlanPdfService>();
        services.AddScoped<FoodService>();
        services.AddScoped<LegacyFoodImportService>();
        services.AddScoped<PackageService>();
        services.AddScoped<ReportingService>();
        services.AddScoped<AdvancedReportingService>();
        services.AddScoped<CurrencyService>();
        services.AddScoped<PaymentAccountService>();
        services.AddScoped<FoodCategoryService>();
        services.AddScoped<ExerciseCategoryService>();
        services.AddScoped<EquipmentService>();
        services.AddScoped<MuscleGroupService>();
        services.AddScoped<ClientProfileService>();
        services.AddScoped<SavedPlanQueryService>();
        services.AddScoped<ExerciseService>();
        services.AddScoped<LegacyExerciseImportService>();
        services.AddScoped<WorkoutPlanService>();
        services.AddScoped<WorkoutTrackingService>();
        services.AddScoped<ProgressService>();
        services.AddScoped<ProgressDashboardService>();
        services.AddScoped<CheckInService>();
        // P14: chat, shared files and event notifications. Handlers run after a save (IDomainEventDispatcher).
        services.AddScoped<ChatService>();
        // P15: calendars and dashboards.
        services.AddScoped<CalendarService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ClientFileService>();
        services.AddScoped<InboxService>();
        services.AddScoped<NotificationCenter>();
        services.AddScoped<EventNotificationScanner>();
        services.AddScoped<PlanNotificationHandler>();
        services.AddScoped<IDomainEventHandler<PlanVersionActivated>>(sp => sp.GetRequiredService<PlanNotificationHandler>());
        services.AddScoped<IDomainEventHandler<ActivePlanVersionSaved>>(sp => sp.GetRequiredService<PlanNotificationHandler>());
        services.AddScoped<IDomainEventHandler<PlanAssigned>>(sp => sp.GetRequiredService<PlanNotificationHandler>());
        services.AddScoped<IDomainEventHandler<CheckInReviewed>, CheckInNotificationHandler>();
        services.AddScoped<IDomainEventHandler<ChatMessageSent>, ChatMessageSentHandler>();
        services.AddScoped<TestDefinitionService>();
        services.AddScoped<FitnessTestService>();
        services.AddScoped<AdherenceService>();
        return services;
    }
}