using CoachHub.Application.FoodTracking;
using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;

namespace CoachHub.Application.Tests.FoodTracking;

/// <summary>Planned versus actual (§9, §10, §11).</summary>
public sealed class NutritionAdherenceTests
{
    [Fact]
    public void Adherence_averages_calories_and_macros_against_the_target()
    {
        var target = new MacroAmounts(2000, 150, 200, 60);

        // Calories 10% under, protein exact, carbohydrates and fat 10% over: 90, 100, 90, 90.
        Assert.Equal(92.5m, NutritionAdherence.Percent(target, new MacroAmounts(1800, 150, 220, 66)));
        Assert.Equal(100m, NutritionAdherence.Percent(target, target));
        // More than double the target scores zero, never below.
        Assert.Equal(0m, NutritionAdherence.Percent(new MacroAmounts(100, 0, 0, 0), new MacroAmounts(250, 40, 0, 0)));
        Assert.Null(NutritionAdherence.Percent(MacroAmounts.Zero, new MacroAmounts(500, 20, 50, 10)));
    }

    [Fact]
    public void Shares_and_weeks()
    {
        Assert.Equal(75m, NutritionAdherence.Share(3, 4));
        Assert.Equal(33.3m, NutritionAdherence.Share(1, 3));
        Assert.Null(NutritionAdherence.Share(0, 0));
        Assert.Equal(new DateOnly(2026, 10, 12), NutritionAdherence.WeekStart(new DateOnly(2026, 10, 13)));
        Assert.Equal(new DateOnly(2026, 10, 12), NutritionAdherence.WeekStart(new DateOnly(2026, 10, 18)));
        Assert.Equal(new DateOnly(2026, 10, 12), NutritionAdherence.WeekStart(new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void The_version_in_force_is_the_one_current_that_day()
    {
        var created = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var plan = DietPlan.Create(Guid.NewGuid(), "Cut", null, Guid.NewGuid(), created);
        var v1 = DietPlanRevision.Create(plan.Id, 1, PlanRevisionStatus.Active, new DateOnly(2026, 9, 1), null, null, created);
        v1.Archive(new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero));
        var v2 = DietPlanRevision.Create(plan.Id, 2, PlanRevisionStatus.Active, new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 31), null, created);
        DietVersion[] versions = [new(plan, v1), new(plan, v2)];

        Assert.Equal(1, NutritionAdherence.InForce(versions, new DateOnly(2026, 10, 4))!.Revision.VersionNumber);
        Assert.Equal(2, NutritionAdherence.InForce(versions, new DateOnly(2026, 10, 5))!.Revision.VersionNumber);
        Assert.Null(NutritionAdherence.InForce(versions, new DateOnly(2026, 8, 31)));
        Assert.Null(NutritionAdherence.InForce(versions, new DateOnly(2027, 1, 1)));

        // A draft is not in force; with several plans the latest start wins.
        var draftPlan = DietPlan.Create(plan.CoachId, "Draft", null, plan.ClientId, created);
        var draft = DietPlanRevision.Create(draftPlan.Id, 1, PlanRevisionStatus.Draft, new DateOnly(2026, 10, 10), null, null, created);
        var bulkPlan = DietPlan.Create(plan.CoachId, "Bulk", null, plan.ClientId, created);
        var bulk = DietPlanRevision.Create(bulkPlan.Id, 1, PlanRevisionStatus.Active, new DateOnly(2026, 10, 10), null, null, created);
        Assert.Equal("Cut", NutritionAdherence.InForce([.. versions, new(draftPlan, draft)], new DateOnly(2026, 10, 12))!.Plan.NameEn);
        Assert.Equal("Bulk", NutritionAdherence.InForce([.. versions, new(draftPlan, draft), new(bulkPlan, bulk)], new DateOnly(2026, 10, 12))!.Plan.NameEn);
    }

    [Fact]
    public void A_period_averages_logged_days_and_counts_logging_up_to_today()
    {
        var target = new MacroAmounts(2000, 150, 200, 60);
        NutritionDayRow[] rows =
        [
            new(new DateOnly(2026, 10, 12), target, target, new MacroAmounts(1800, 150, 220, 66), 92.5m, 100m, 4, true),
            new(new DateOnly(2026, 10, 13), target, target, new MacroAmounts(2000, 150, 200, 60), 100m, 50m, 2, false),
            new(new DateOnly(2026, 10, 14), target, target, MacroAmounts.Zero, null, 0m, 0, false),
            new(new DateOnly(2026, 10, 15), target, target, MacroAmounts.Zero, null, 0m, 0, false),
        ];

        var summary = NutritionAdherence.Summarize(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 18), rows, new DateOnly(2026, 10, 14));

        Assert.Equal((3, 2, 1), (summary.Days, summary.LoggedDays, summary.CompletedDays));
        Assert.Equal(new MacroAmounts(1900, 150, 210, 63), summary.AverageConsumed);
        Assert.Equal((96.3m, 66.7m), (summary.AdherencePercent!.Value, summary.LoggingAdherencePercent!.Value));
        Assert.Equal(target, summary.AverageTarget);
    }
}
