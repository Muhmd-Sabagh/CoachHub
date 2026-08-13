using CoachHub.Application.Auth.Login;
using CoachHub.Application.Media;
using CoachHub.Application.ReferenceData;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<MediaService>();
        services.AddScoped<PackageService>();
        services.AddScoped<CurrencyService>();
        services.AddScoped<PaymentAccountService>();
        services.AddScoped<FoodCategoryService>();
        services.AddScoped<ExerciseCategoryService>();
        return services;
    }
}