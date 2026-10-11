using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Tenancy;
using CoachHub.Domain.Coaches;

namespace CoachHub.Application.Tests.Coaches;

public sealed class CoachOwnershipTests
{
    [Fact]
    public async Task A_coach_always_owns_what_they_create()
    {
        var coachId = Guid.NewGuid();
        var ownership = new CoachOwnership(FakeCurrentUser.ForCoach(coachId), new FakeCoachRepository());

        Assert.Equal(coachId, await ownership.ForNewRecordAsync(null, CancellationToken.None));
        Assert.Equal(coachId, await ownership.ForNewRecordAsync(coachId, CancellationToken.None));
    }

    [Fact]
    public async Task A_coach_cannot_create_records_for_another_coach()
    {
        var ownership = new CoachOwnership(FakeCurrentUser.ForCoach(Guid.NewGuid()), new FakeCoachRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ownership.ForNewRecordAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task An_administrator_must_name_an_existing_active_coach()
    {
        var active = Coach.Create("Active", DateTimeOffset.UtcNow);
        var inactive = Coach.Create("Inactive", DateTimeOffset.UtcNow);
        inactive.Update("Inactive", isActive: false);
        var ownership = new CoachOwnership(FakeCurrentUser.Administrator(), new FakeCoachRepository(active, inactive));

        var missing = await Assert.ThrowsAsync<ValidationException>(() =>
            ownership.ForNewRecordAsync(null, CancellationToken.None));
        Assert.Contains("coachId", missing.Errors);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            ownership.ForNewRecordAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() =>
            ownership.ForNewRecordAsync(inactive.Id, CancellationToken.None));
        Assert.Equal(active.Id, await ownership.ForNewRecordAsync(active.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData(TenantScope.None)]
    [InlineData(TenantScope.Client)]
    public async Task Clients_and_anonymous_callers_cannot_create_coach_records(TenantScope scope)
    {
        var ownership = new CoachOwnership(
            new FakeCurrentUser(scope, Guid.NewGuid(), Guid.NewGuid()), new FakeCoachRepository());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            ownership.ForNewRecordAsync(null, CancellationToken.None));
    }
}
