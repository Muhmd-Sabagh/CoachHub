using CoachHub.Application.Auth.Login;
using CoachHub.Application.Media;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<MediaService>();
        return services;
    }
}
