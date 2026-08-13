using CoachHub.Infrastructure;
using CoachHub.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Persistence;

public sealed class CoachHubDbContextTests
{
    [Fact]
    public void Baseline_model_contains_no_legacy_or_feature_entities()
    {
        using var context = CreateContext();

        Assert.Empty(context.Model.GetEntityTypes());
    }

    [Fact]
    public void Infrastructure_registers_the_sql_server_context()
    {
        var values = new Dictionary<string, string?>
        {
            [$"ConnectionStrings:{DatabaseOptions.ConnectionStringName}"] =
                DatabaseOptions.DevelopmentConnectionString
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var context = provider.GetRequiredService<CoachHubDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
    }

    private static CoachHubDbContext CreateContext()
    {
        var factory = new CoachHubDbContextFactory();
        return factory.CreateDbContext([]);
    }
}
