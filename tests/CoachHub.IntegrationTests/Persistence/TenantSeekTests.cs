using CoachHub.Application.Auditing;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Infrastructure.Auditing;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.IntegrationTests.Persistence;

/// <summary>
/// P18 query plans: a coach's audit trail repeats the coach's key as a plain predicate, so SQL Server can seek
/// <c>IX_AuditEntries_CoachId_OccurredAt</c> instead of scanning past the parameterised tenant filter. Other callers
/// keep the filter alone.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class TenantSeekTests(SqlServerTestDatabase database)
{
    [Fact]
    public async Task A_coach_query_carries_the_coach_key_as_a_plain_predicate()
    {
        var coachId = Guid.NewGuid();
        var sql = await AuditSqlAsync(new SeekUser(TenantScope.Coach, coachId));

        Assert.Matches(@"\) AND \[a\]\.\[CoachId\] = @", sql);
    }

    [Theory]
    [InlineData(TenantScope.Unrestricted)]
    [InlineData(TenantScope.Client)]
    public async Task Other_callers_rely_on_the_tenant_filter_alone(TenantScope scope)
    {
        var sql = await AuditSqlAsync(new SeekUser(scope, scope == TenantScope.Client ? Guid.NewGuid() : null));

        Assert.DoesNotMatch(@"\) AND \[a\]\.\[CoachId\] = @", sql);
    }

    private async Task<string> AuditSqlAsync(ICurrentUser user)
    {
        var commands = new List<string>();
        await using var context = new CoachHubDbContext(
            new DbContextOptionsBuilder<CoachHubDbContext>()
                .UseSqlServer(database.ConnectionString)
                .LogTo(message => { if (message.Contains("FROM [AuditEntries]")) commands.Add(message); },
                    [DbLoggerCategory.Database.Command.Name])
                .Options,
            currentUser: user);

        await new AuditQueryRepository(context).ListAsync(new AuditQuery().Normalize(), CancellationToken.None);
        return string.Join("\n", commands);
    }

    private sealed class SeekUser(TenantScope scope, Guid? coachId) : ICurrentUser
    {
        public Guid? UserId => Guid.NewGuid();
        public string? DisplayName => "Seek";
        public string? Role => scope.ToString();
        public TenantScope Scope => scope;
        public Guid? CoachId => coachId;
        public Guid? ClientId => scope == TenantScope.Client ? Guid.NewGuid() : null;
        public bool IsAdministrator => scope == TenantScope.Unrestricted;
    }
}
