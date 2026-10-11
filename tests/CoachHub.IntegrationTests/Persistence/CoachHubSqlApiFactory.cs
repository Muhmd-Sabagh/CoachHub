using CoachHub.IntegrationTests.Auth;
using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoachHub.IntegrationTests.Persistence;

/// <summary>
/// Hosts the API against a migrated SQL Server database. Use for behavior that EF InMemory
/// cannot prove: query filters, constraints, cascades, and raw migration SQL.
/// </summary>
public sealed class CoachHubSqlApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqlServerTestDatabase _database = new();

    public SqlServerTestDatabase Database => _database;

    public Task InitializeAsync() => _database.InitializeAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:BootstrapAdmin:Enabled"] = "true",
                ["Authentication:BootstrapAdmin:Email"] = CoachHubApiFactory.AdminEmail,
                ["Authentication:BootstrapAdmin:Password"] = CoachHubApiFactory.AdminPassword,
                ["Authentication:BootstrapAdmin:DisplayName"] = "CoachHub Administrator"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<CoachHubDbContext>();
            services.RemoveAll<DbContextOptions<CoachHubDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<CoachHubDbContext>>();
            services.AddDbContext<CoachHubDbContext>(options =>
                options.UseSqlServer(
                    _database.ConnectionString,
                    sqlServer => sqlServer.MigrationsAssembly(typeof(CoachHubDbContext).Assembly.FullName)));
        });
    }
}
