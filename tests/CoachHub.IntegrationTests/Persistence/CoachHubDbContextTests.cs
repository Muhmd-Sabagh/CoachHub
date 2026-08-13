using CoachHub.Infrastructure;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoachHub.IntegrationTests.Persistence;

public sealed class CoachHubDbContextTests
{
    [Fact]
    public void Model_contains_identity_but_no_legacy_business_entities()
    {
        using var context = CreateContext();
        var entityTypes = context.Model.GetEntityTypes().Select(type => type.ClrType).ToArray();

        Assert.Contains(typeof(User), entityTypes);
        Assert.Contains(typeof(Role), entityTypes);
        Assert.Contains(typeof(UserRole), entityTypes);
        Assert.DoesNotContain(entityTypes, type => type.Name is
            "ClientAssessment" or "ClientUpdate" or "GymDbContext");
    }

    [Fact]
    public void Infrastructure_registers_the_sql_server_context()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:" + DatabaseOptions.ConnectionStringName] =
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
