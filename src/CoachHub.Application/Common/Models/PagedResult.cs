namespace CoachHub.Application.Common.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount)
{
    public long TotalPages => PageSize <= 0
        ? 0
        : (long)Math.Ceiling(TotalCount / (double)PageSize);
}
