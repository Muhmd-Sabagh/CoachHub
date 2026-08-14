using CoachHub.Application.Auditing;
using CoachHub.Application.Assessments;
using CoachHub.Application.Assessments.Importing;
using CoachHub.Application.Auth;
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
using CoachHub.Infrastructure.Auditing;
using CoachHub.Infrastructure.Assessments;
using CoachHub.Infrastructure.Assessments.Importing;
using CoachHub.Infrastructure.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Billing;
using CoachHub.Infrastructure.Communications;
using CoachHub.Infrastructure.Clients;
using CoachHub.Infrastructure.DietPlanning;
using CoachHub.Infrastructure.Media;
using CoachHub.Infrastructure.Pdf;
using CoachHub.Infrastructure.PlanDelivery;
using CoachHub.Infrastructure.Persistence;
using CoachHub.Infrastructure.Nutrition;
using CoachHub.Infrastructure.ReferenceData;
using CoachHub.Infrastructure.Reporting;
using CoachHub.Infrastructure.SavedPlans;
using CoachHub.Infrastructure.Training;
using CoachHub.Infrastructure.WorkoutPlanning;
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
            .AddEntityFrameworkStores<CoachHubDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AuthExperienceOptions>(configuration.GetSection(AuthExperienceOptions.SectionName));
        services.Configure<CommunicationOptions>(configuration.GetSection(CommunicationOptions.SectionName));
        services.Configure<AdminBootstrapOptions>(
            configuration.GetSection(AdminBootstrapOptions.SectionName));

        services.AddScoped<IIdentityGateway, IdentityGateway>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IBillingRepository, BillingRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IPlanDeliveryRepository, PlanDeliveryRepository>();
        services.AddScoped<INotificationSender, EmailNotificationSender>();
        services.AddHttpClient<WhatsAppNotificationSender>();
        services.AddScoped<INotificationSender>(provider => provider.GetRequiredService<WhatsAppNotificationSender>());
        services.AddHostedService<NotificationDispatcher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<AdminBootstrapper>();

        AddMedia(services, configuration, allowLocalMediaStorage);
        services.AddScoped<IAuditQueryRepository, AuditQueryRepository>();
        services.AddScoped(typeof(IReferenceRepository<>), typeof(ReferenceRepository<>));
        services.AddScoped<ISavedPlanQueryRepository, SavedPlanQueryRepository>();
        services.AddScoped<IFoodRepository, FoodRepository>();
        services.AddScoped<IExerciseRepository, ExerciseRepository>();
        services.AddScoped<IWorkoutPlanRepository, WorkoutPlanRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<IAdvancedReportingRepository, AdvancedReportingRepository>();
        services.AddScoped<IDietPlanRepository, DietPlanRepository>();
        services.AddScoped<IFormRepository, FormRepository>();
        services.AddScoped<IFormImportRepository, FormImportRepository>();
        services.AddSingleton<IAssessmentWorkbookParser, XlsxAssessmentWorkbookParser>();
        services.AddSingleton<IClientCodeGenerator, SecureClientCodeGenerator>();
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

        var fileSystem = string.Equals(options.Provider, "FileSystem", StringComparison.OrdinalIgnoreCase);
        var s3 = string.Equals(options.Provider, "S3", StringComparison.OrdinalIgnoreCase);
        if (!fileSystem && !s3)
        {
            throw new InvalidOperationException("Media Provider must be FileSystem or S3.");
        }

        if (fileSystem && !allowLocalMediaStorage)
        {
            throw new InvalidOperationException(
                "FileSystem media storage is allowed only in Development or isolated tests.");
        }

        if (fileSystem && string.IsNullOrWhiteSpace(options.StorageRoot))
        {
            throw new InvalidOperationException("Media StorageRoot is required.");
        }

        services.AddOptions<MediaStorageOptions>()
            .Bind(configuration.GetSection(MediaStorageOptions.SectionName))
            .Validate(value => fileSystem
                ? !string.IsNullOrWhiteSpace(value.StorageRoot)
                : !string.IsNullOrWhiteSpace(value.BucketName) &&
                  !string.IsNullOrWhiteSpace(value.AccessKey) &&
                  !string.IsNullOrWhiteSpace(value.SecretKey),
                "Selected media provider configuration is incomplete.")
            .ValidateOnStart();
        services.AddScoped<FileSystemMediaStorage>();
        services.AddScoped<S3MediaStorage>();
        services.AddScoped<IMediaStorage>(provider => fileSystem
            ? provider.GetRequiredService<FileSystemMediaStorage>()
            : provider.GetRequiredService<S3MediaStorage>());
        services.AddScoped<IMediaRepository, MediaRepository>();
        services.AddScoped<IPlanPdfClientRepository, PlanPdfClientRepository>();
        services.AddSingleton<IPlanPdfRenderer, QuestPlanPdfRenderer>();
    }
}
