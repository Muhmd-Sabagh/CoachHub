using CoachHub.Application.Auditing;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;

namespace CoachHub.Application.Tests.Auditing;

public sealed class AuditQueryServiceTests
{
    [Fact]
    public async Task List_normalizes_paging_and_explicit_filters()
    {
        var repository = new StubRepository();
        var service = new AuditQueryService(repository);

        await service.ListAsync(new AuditQuery
        {
            PageNumber = 0,
            PageSize = 500,
            SearchTerm = "  Coach  ",
            EntityType = " Client ",
            SortBy = " OCCURREDAT "
        }, CancellationToken.None);

        Assert.NotNull(repository.Query);
        Assert.Equal(1, repository.Query.PageNumber);
        Assert.Equal(100, repository.Query.PageSize);
        Assert.Equal("Coach", repository.Query.SearchTerm);
        Assert.Equal("Client", repository.Query.EntityType);
        Assert.Equal("occurredat", repository.Query.SortBy);
    }

    [Fact]
    public async Task List_rejects_invalid_date_range_and_sort_field()
    {
        var service = new AuditQueryService(new StubRepository());

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.ListAsync(new AuditQuery
            {
                OccurredFrom = DateTimeOffset.UtcNow,
                OccurredTo = DateTimeOffset.UtcNow.AddDays(-1),
                SortBy = "payload"
            }, CancellationToken.None));

        Assert.Contains("occurredAt", exception.Errors);
        Assert.Contains("sortBy", exception.Errors);
    }

    private sealed class StubRepository : IAuditQueryRepository
    {
        public AuditQuery? Query { get; private set; }
        public Task<PagedResult<AuditRecord>> ListAsync(AuditQuery query, CancellationToken cancellationToken)
        {
            Query = query;
            return Task.FromResult(new PagedResult<AuditRecord>([], query.PageNumber, query.PageSize, 0));
        }
    }
}
