using CoachHub.Application.Assessments;
using CoachHub.Application.Assessments.Importing;
using CoachHub.Application.Auth.Login;
using CoachHub.Application.Clients;
using CoachHub.Application.Media;
using CoachHub.Application.Nutrition;
using CoachHub.Application.ReferenceData;
using CoachHub.Application.Training;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<FormAdminService>();
        services.AddScoped<FormSubmissionService>();
        services.AddScoped<AssessmentImportService>();
        services.AddScoped<ClientService>();
        services.AddScoped<SubscriptionService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<MediaService>();
        services.AddScoped<FoodService>();
        services.AddScoped<LegacyFoodImportService>();
        services.AddScoped<PackageService>();
        services.AddScoped<CurrencyService>();
        services.AddScoped<PaymentAccountService>();
        services.AddScoped<FoodCategoryService>();
        services.AddScoped<ExerciseCategoryService>();
        services.AddScoped<ExerciseService>();
        services.AddScoped<LegacyExerciseImportService>();
        return services;
    }
}