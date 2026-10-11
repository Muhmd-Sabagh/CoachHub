using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Coaches;

namespace CoachHub.Application.Tests;

internal sealed class FakeCurrentUser(
    TenantScope scope,
    Guid? coachId = null,
    Guid? clientId = null,
    bool isAdministrator = false) : ICurrentUser
{
    public Guid? UserId { get; } = Guid.NewGuid();
    public string? DisplayName => "Test user";
    public string? Role => isAdministrator ? "Administrator" : scope.ToString();
    public TenantScope Scope => scope;
    public Guid? CoachId => coachId;
    public Guid? ClientId => clientId;
    public bool IsAdministrator => isAdministrator;

    public static FakeCurrentUser Administrator() => new(TenantScope.Unrestricted, isAdministrator: true);
    public static FakeCurrentUser ForCoach(Guid coachId) => new(TenantScope.Coach, coachId);
}

internal sealed class FakeCoachRepository(params Coach[] coaches) : ICoachRepository
{
    public Task<Coach?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(coaches.SingleOrDefault(coach => coach.Id == id));
}

internal static class TestTenancy
{
    public static readonly Guid CoachId = Guid.NewGuid();

    /// <summary>Ownership as seen by a signed-in coach: new records belong to <see cref="CoachId"/>.</summary>
    public static CoachOwnership CoachOwnership() =>
        new(FakeCurrentUser.ForCoach(CoachId), new FakeCoachRepository());
}
