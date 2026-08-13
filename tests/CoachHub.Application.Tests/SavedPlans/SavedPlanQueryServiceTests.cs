using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Application.SavedPlans;

namespace CoachHub.Application.Tests.SavedPlans;

public sealed class SavedPlanQueryServiceTests
{
    [Fact]
    public async Task List_normalizes_paging_and_explicit_search_values()
    {
        var repository = new CapturingRepository();
        var service = new SavedPlanQueryService(repository);
        await service.ListAsync(new SavedPlanQuery
        {
            PageNumber = -2, PageSize = 500, PlanName = "  plan  ", SortBy = " NAME "
        }, CancellationToken.None);

        Assert.Equal(1, repository.Query!.PageNumber);
        Assert.Equal(100, repository.Query.PageSize);
        Assert.Equal("plan", repository.Query.PlanName);
        Assert.Equal("name", repository.Query.SortBy);
    }

    [Fact]
    public async Task List_rejects_incompatible_type_specific_ranges()
    {
        var service = new SavedPlanQueryService(new CapturingRepository());
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAsync(new SavedPlanQuery
        {
            MinCalories = 100, MinWorkoutDays = 2
        }, CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => service.ListAsync(new SavedPlanQuery
        {
            PlanType = SavedPlanType.Workout, MaxProtein = 200
        }, CancellationToken.None));
    }

    private sealed class CapturingRepository : ISavedPlanQueryRepository
    {
        public SavedPlanQuery? Query { get; private set; }
        public Task<PagedResult<SavedPlanSummary>> ListAsync(SavedPlanQuery query, CancellationToken cancellationToken)
        {
            Query = query;
            return Task.FromResult(new PagedResult<SavedPlanSummary>([], query.PageNumber, query.PageSize, 0));
        }
    }
}
