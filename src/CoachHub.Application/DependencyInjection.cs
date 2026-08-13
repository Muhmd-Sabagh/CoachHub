using CoachHub.Application.Auth.Login;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<LoginCommandHandler>();
        return services;
    }
}
