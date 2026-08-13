using CoachHub.Application.Common.Models;

namespace CoachHub.Application.Tests.Common;

public sealed class PagedRequestTests
{
    [Fact]
    public void Normalize_enforces_bounds_and_trims_search_term()
    {
        var request = new PagedRequest
        {
            PageNumber = 0,
            PageSize = 500,
            SearchTerm = "  client  "
        };

        var normalized = request.Normalize();

        Assert.Equal(1, normalized.PageNumber);
        Assert.Equal(PagedRequest.MaximumPageSize, normalized.PageSize);
        Assert.Equal("client", normalized.SearchTerm);
    }
}
