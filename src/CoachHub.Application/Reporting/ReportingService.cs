using CoachHub.Application.Common.Exceptions;

namespace CoachHub.Application.Reporting;

public sealed class ReportingService(IReportingRepository repository, TimeProvider timeProvider)
{
    public Task<OperationalReport> GetAsync(
        ReportingQuery query,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var normalized = query.Normalize(today);
        var from = normalized.From!.Value;
        var to = normalized.To!.Value;
        var errors = new Dictionary<string, string[]>();

        if (from > to)
            errors["period"] = ["Report start date cannot be after the end date."];
        if (to.DayNumber - from.DayNumber > 365)
            errors["period"] = ["Reporting periods cannot exceed 366 days."];
        if (errors.Count > 0)
            throw new ValidationException(errors);

        return repository.GetAsync(from, to, today, cancellationToken);
    }
}