using CoachHub.Application.Auth;
using CoachHub.Infrastructure.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string '" + DatabaseOptions.ConnectionStringName + "' is required.");
        }

        services.AddDbContext<CoachHubDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer.MigrationsAssembly(typeof(CoachHubDbContext).Assembly.FullName)));

        services
            .AddIdentityCore<User>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<Role>()
            .AddEntityFrameworkStores<CoachHubDbContext>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminBootstrapOptions>(
            configuration.GetSection(AdminBootstrapOptions.SectionName));

        services.AddScoped<IIdentityGateway, IdentityGateway>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<AdminBootstrapper>();

        return services;
    }
}
