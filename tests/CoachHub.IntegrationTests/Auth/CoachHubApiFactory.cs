using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoachHub.IntegrationTests.Auth;

public sealed class CoachHubApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@coachhub.test";
    public const string AdminPassword = "SecurePassword!123";

    private readonly string _databaseName = "CoachHubAuthTests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:BootstrapAdmin:Enabled"] = "true",
                ["Authentication:BootstrapAdmin:Email"] = AdminEmail,
                ["Authentication:BootstrapAdmin:Password"] = AdminPassword,
                ["Authentication:BootstrapAdmin:DisplayName"] = "CoachHub Administrator"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<CoachHubDbContext>();
            services.RemoveAll<DbContextOptions<CoachHubDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CoachHubDbContext>>();
            services.AddDbContext<CoachHubDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
