using CoachHub.Application.Auth;
using CoachHub.Application.Media;
using CoachHub.Infrastructure.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Media;
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
        IConfiguration configuration,
        bool allowLocalMediaStorage = false)
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

        AddMedia(services, configuration, allowLocalMediaStorage);
        return services;
    }

    private static void AddMedia(
        IServiceCollection services,
        IConfiguration configuration,
        bool allowLocalMediaStorage)
    {
        var options = configuration
            .GetSection(MediaStorageOptions.SectionName)
            .Get<MediaStorageOptions>()
            ?? throw new InvalidOperationException("Media storage configuration is required.");

        if (!string.Equals(options.Provider, "FileSystem", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "No supported external media provider is configured. FileSystem is development-only.");
        }

        if (!allowLocalMediaStorage)
        {
            throw new InvalidOperationException(
                "FileSystem media storage is allowed only in Development or isolated tests.");
        }

        if (string.IsNullOrWhiteSpace(options.StorageRoot))
        {
            throw new InvalidOperationException("Media StorageRoot is required.");
        }

        services.Configure<MediaStorageOptions>(
            configuration.GetSection(MediaStorageOptions.SectionName));
        services.AddScoped<IMediaStorage, FileSystemMediaStorage>();
        services.AddScoped<IMediaRepository, MediaRepository>();
    }
}
