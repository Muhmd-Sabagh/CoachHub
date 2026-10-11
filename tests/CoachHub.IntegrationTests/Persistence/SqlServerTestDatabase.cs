using CoachHub.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.IntegrationTests.Persistence;

/// <summary>
/// A uniquely named SQL Server database with all migrations applied, dropped on disposal.
/// Uses the COACHHUB_TEST_SQL connection string when set (CI), otherwise the local SQL Server
/// default from <see cref="DatabaseOptions.DevelopmentConnectionString"/>.
/// </summary>
public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    public const string ConnectionStringVariable = "COACHHUB_TEST_SQL";

    public SqlServerTestDatabase()
    {
        var server = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        var builder = new SqlConnectionStringBuilder(
            string.IsNullOrWhiteSpace(server) ? DatabaseOptions.DevelopmentConnectionString : server)
        {
            InitialCatalog = "CoachHubTests_" + Guid.NewGuid().ToString("N")
        };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    public CoachHubDbContext CreateContext() => new(
        new DbContextOptionsBuilder<CoachHubDbContext>()
            .UseSqlServer(
                ConnectionString,
                sqlServer => sqlServer.MigrationsAssembly(typeof(CoachHubDbContext).Assembly.FullName))
            .Options);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerTestDatabase>
{
    public const string Name = "SQL Server";
}
