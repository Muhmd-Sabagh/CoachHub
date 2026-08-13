namespace CoachHub.Infrastructure.Persistence;

public static class DatabaseOptions
{
    public const string ConnectionStringName = "CoachHubDatabase";

    public const string DevelopmentConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=CoachHub;Trusted_Connection=True;" +
        "MultipleActiveResultSets=true;TrustServerCertificate=True";
}
