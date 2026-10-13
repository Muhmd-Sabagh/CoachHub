using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoachHub.IntegrationTests.Auth;

public sealed class CoachHubApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@coachhub.test";
    public const string AdminPassword = "SecurePassword!123";

    private readonly string _databaseName = "CoachHubAuthTests-" + Guid.NewGuid();
    private readonly InMemoryDatabaseRoot _root = new();

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
            services.AddDbContext<CoachHubDbContext>(options => Configure(options));
            // The store starts with the model's seed data (muscle groups), as migrations do on SQL Server. It is
            // created before the app starts, so the administrator bootstrap does not create it first without them.
            using var seed = new CoachHubDbContext(Configure(new DbContextOptionsBuilder<CoachHubDbContext>()).Options);
            seed.Database.EnsureCreated();
        });
    }

    // InMemory has no transactions; services that open one (coach creation) still run here.
    private DbContextOptionsBuilder<CoachHubDbContext> Configure(DbContextOptionsBuilder options) =>
        (DbContextOptionsBuilder<CoachHubDbContext>)options
            .UseInMemoryDatabase(_databaseName, _root)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
}
