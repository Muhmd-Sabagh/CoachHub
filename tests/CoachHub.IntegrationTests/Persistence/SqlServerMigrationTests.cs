using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
public sealed class SqlServerMigrationTests(SqlServerTestDatabase database)
{
    [Fact]
    public async Task Migrations_apply_on_sql_server_and_match_the_current_model()
    {
        await using var context = database.CreateContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The EF model has changes that are not captured in a migration. Add a migration.");
    }

    [Fact]
    public async Task Seeded_placeholder_administrator_matches_the_bootstrap_guard_and_is_unusable()
    {
        await using var context = database.CreateContext();

        var seeded = await context.Users.AsNoTracking()
            .SingleAsync(user => user.Id == SeedIdentity.AdministratorUserId);

        Assert.Equal(SeedIdentity.AdministratorEmail, seeded.Email);
        Assert.False(seeded.IsActive);
        Assert.Null(seeded.PasswordHash);
        Assert.False(seeded.EmailConfirmed);
        Assert.NotNull(seeded.LockoutEnd);
    }
}
