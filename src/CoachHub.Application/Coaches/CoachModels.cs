using CoachHub.Domain.Coaches;

namespace CoachHub.Application.Coaches;

public sealed record CoachResponse(
    Guid Id,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    Guid? UserId,
    string? Email,
    string? UserName,
    int ClientCount);

/// <summary>Creates a coach (tenant) together with its login account.</summary>
public sealed record CoachCreateInput(string DisplayName, string Email, string? UserName, string? Password);

public sealed record CoachUpdateInput(string DisplayName, bool IsActive);

public sealed record CoachQuery
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }

    public CoachQuery Normalize() => this with
    {
        PageNumber = Math.Max(1, PageNumber),
        PageSize = Math.Clamp(PageSize, 1, 100),
        SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim()
    };
}

public interface ICoachRepository
{
    Task<Coach?> FindAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>Coach administration spans the coach tenant and its Identity account, so it lives in Infrastructure.</summary>
public interface ICoachAdministrationService
{
    Task<Common.Models.PagedResult<CoachResponse>> ListAsync(CoachQuery query, CancellationToken cancellationToken);
    Task<CoachResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<CoachResponse> CreateAsync(CoachCreateInput input, CancellationToken cancellationToken);
    Task<CoachResponse> UpdateAsync(Guid id, CoachUpdateInput input, CancellationToken cancellationToken);
}
