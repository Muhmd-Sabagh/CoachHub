using CoachHub.Application.CheckIns;
using CoachHub.Application.Common.Exceptions;

namespace CoachHub.Application.Dashboards;

/// <summary>Revenue (D6) summarised from settled payments and refunds. Pure.</summary>
public static class RevenueCalculator
{
    public static IReadOnlyList<RevenueLine> By(IEnumerable<RevenueRow> rows, Func<RevenueRow, (string Key, Guid? Id, string Label)> group) =>
        rows.GroupBy(row => (Group: group(row), row.CurrencyCode))
            .Select(x =>
            {
                var settled = x.Where(row => !row.IsRefund).Sum(row => row.Amount);
                var refunded = x.Where(row => row.IsRefund).Sum(row => row.Amount);
                return new RevenueLine(x.Key.Group.Key, x.Key.Group.Id, x.Key.Group.Label, x.Key.CurrencyCode,
                    x.Count(row => !row.IsRefund), x.Count(row => row.IsRefund), settled, refunded, settled - refunded);
            })
            .OrderBy(x => x.CurrencyCode).ThenByDescending(x => x.Net).ThenBy(x => x.Label)
            .ToArray();

    public static IReadOnlyList<RevenueLine> ByCurrency(IEnumerable<RevenueRow> rows) =>
        By(rows, row => (row.CurrencyId.ToString(), row.CurrencyId, row.CurrencyCode));

    public static IReadOnlyList<RevenueLine> ByCoach(IEnumerable<RevenueRow> rows) =>
        By(rows, row => ($"{row.CoachId}", row.CoachId, row.CoachName));

    public static IReadOnlyList<RevenueLine> ByPackage(IEnumerable<RevenueRow> rows) =>
        By(rows, row => (row.PackageId?.ToString() ?? "none", row.PackageId, row.PackageName));

    public static IReadOnlyList<RevenueLine> ByPaymentAccount(IEnumerable<RevenueRow> rows) =>
        By(rows, row => (row.PaymentAccountId?.ToString() ?? "none", row.PaymentAccountId, row.PaymentAccountName));
}

/// <summary>
/// The coach dashboard (§38) and the administrator dashboard (§48), both reporting revenue (D6). Tenant filters
/// keep a coach to their own business; administrators see the platform.
/// </summary>
public sealed class DashboardService(IDashboardRepository repository, TimeProvider timeProvider)
{
    public const int MaximumRangeDays = 366;

    public async Task<CoachDashboard> CoachAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        var (from, to, today) = Period(query);
        var figures = await repository.CoachAsync(from, to, today, CheckInService.OpensDaysBefore, cancellationToken);
        var revenue = await repository.RevenueRowsAsync(from, to, cancellationToken);
        return new CoachDashboard(from, to, today, figures.Clients, figures.Statuses, figures.CheckIns,
            figures.ExpiredPrograms.Count, figures.ExpiredPrograms.Take(20).ToArray(), figures.RecentActivity,
            RevenueCalculator.ByCurrency(revenue));
    }

    public async Task<AdminDashboard> AdminAsync(DashboardQuery query, CancellationToken cancellationToken)
    {
        var (from, to, today) = Period(query);
        var figures = await repository.AdminAsync(from, to, today, cancellationToken);
        var revenue = await repository.RevenueRowsAsync(from, to, cancellationToken);
        return new AdminDashboard(from, to, today, figures.Clients, figures.Coaches, figures.ActiveCoaches, figures.ActiveUsers,
            figures.NewUsers, figures.ExpiredSubscriptions, RevenueCalculator.ByCurrency(revenue), RevenueCalculator.ByCoach(revenue),
            figures.UserActivity, figures.Usage);
    }

    private (DateOnly From, DateOnly To, DateOnly Today) Period(DashboardQuery query)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var to = query.To ?? today;
        var from = query.From ?? to.AddDays(-29);
        if (from > to) throw Validation("period", "The start date cannot be after the end date.");
        if (to.DayNumber - from.DayNumber >= MaximumRangeDays) throw Validation("period", $"A period covers at most {MaximumRangeDays} days.");
        return (from, to, today);
    }

    private static ValidationException Validation(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}
