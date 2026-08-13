using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Reporting;

namespace CoachHub.Application.Tests.Reporting;

public sealed class ReportingServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Default_period_is_trailing_thirty_days_ending_today()
    {
        var repository = new CapturingRepository();
        var service = new ReportingService(repository, new FixedTimeProvider(Now));

        await service.GetAsync(new ReportingQuery(), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 15), repository.From);
        Assert.Equal(new DateOnly(2026, 8, 13), repository.To);
        Assert.Equal(new DateOnly(2026, 8, 13), repository.Today);
    }

    [Fact]
    public async Task Invalid_or_overlong_period_is_rejected()
    {
        var service = new ReportingService(
            new CapturingRepository(),
            new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<ValidationException>(() => service.GetAsync(
            new ReportingQuery
            {
                From = new DateOnly(2026, 8, 14),
                To = new DateOnly(2026, 8, 13)
            },
            CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() => service.GetAsync(
            new ReportingQuery
            {
                From = new DateOnly(2025, 1, 1),
                To = new DateOnly(2026, 8, 13)
            },
            CancellationToken.None));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CapturingRepository : IReportingRepository
    {
        public DateOnly From { get; private set; }
        public DateOnly To { get; private set; }
        public DateOnly Today { get; private set; }

        public Task<OperationalReport> GetAsync(
            DateOnly from,
            DateOnly to,
            DateOnly today,
            CancellationToken cancellationToken)
        {
            From = from;
            To = to;
            Today = today;
            return Task.FromResult(new OperationalReport(
                from,
                to,
                today,
                new ClientMetrics(0, 0, 0, 0, 0, 0),
                new WorkflowMetrics(0, 0),
                new AssessmentMetrics(0, 0),
                new PlanMetrics(0, 0),
                [],
                [],
                [],
                []));
        }
    }
}