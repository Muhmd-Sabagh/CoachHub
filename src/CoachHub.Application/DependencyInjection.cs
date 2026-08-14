using CoachHub.Application.Auditing;
using CoachHub.Application.Assessments;
using CoachHub.Application.Assessments.Importing;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Billing;
using CoachHub.Application.Communications;
using CoachHub.Application.Clients;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Media;
using CoachHub.Application.Pdf;
using CoachHub.Application.PlanDelivery;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Reporting;
using CoachHub.Application.SavedPlans;
using CoachHub.Application.Training;
using CoachHub.Application.WorkoutPlanning;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuditQueryService>();
        services.AddScoped<LoginCommandHandler>();
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
        services.AddScoped<SavedPlanQueryService>();
        services.AddScoped<ExerciseService>();
        services.AddScoped<LegacyExerciseImportService>();
        services.AddScoped<WorkoutPlanService>();
        return services;
    }
}