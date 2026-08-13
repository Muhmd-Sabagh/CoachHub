using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CoachHub.Infrastructure.Persistence;

public sealed class CoachHubDbContextFactory : IDesignTimeDbContextFactory<CoachHubDbContext>
{
    public CoachHubDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__CoachHubDatabase")
            ?? DatabaseOptions.DevelopmentConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<CoachHubDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sqlServer => sqlServer.MigrationsAssembly(typeof(CoachHubDbContext).Assembly.FullName));

        return new CoachHubDbContext(optionsBuilder.Options);
    }
}
