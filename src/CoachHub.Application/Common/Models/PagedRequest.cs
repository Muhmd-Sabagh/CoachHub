namespace CoachHub.Application.Common.Models;

public sealed record PagedRequest
{
    public const int MaximumPageSize = 100;

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? SearchTerm { get; init; }

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }

    public PagedRequest Normalize()
    {
        return this with
        {
            PageNumber = Math.Max(1, PageNumber),
            PageSize = Math.Clamp(PageSize, 1, MaximumPageSize),
            SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim()
        };
    }
}
