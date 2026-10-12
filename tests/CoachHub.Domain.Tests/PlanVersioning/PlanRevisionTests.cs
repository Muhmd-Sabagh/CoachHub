using CoachHub.Domain.Common;
using CoachHub.Domain.DietPlanning;
using CoachHub.Domain.PlanDelivery;
using CoachHub.Domain.WorkoutPlanning;

namespace CoachHub.Domain.Tests.PlanVersioning;

public sealed class PlanRevisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly October = new(2026, 10, 1);

    [Fact]
    public void New_version_records_number_status_program_dates_and_author()
    {
        var author = Guid.NewGuid();
        var revision = DietPlanRevision.Create(Guid.NewGuid(), 2, PlanRevisionStatus.Draft,
            October, October.AddDays(27), author, Now);

        Assert.Equal(2, revision.VersionNumber);
        Assert.Equal(PlanRevisionStatus.Draft, revision.Status);
        Assert.Equal(new DateOnly(2026, 10, 28), revision.EndDate);
        Assert.Equal(author, revision.CreatedByUserId);
        Assert.Equal(Now, revision.UpdatedAt);
        Assert.False(revision.IsArchived);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Version_numbers_start_at_one(int number) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkoutPlanRevision.Create(Guid.NewGuid(), number, PlanRevisionStatus.Active, null, null, null, Now));

    [Fact]
    public void End_date_cannot_precede_start_or_run_past_ten_years()
    {
        Assert.Throws<ArgumentException>(() => DietPlanRevision.Create(Guid.NewGuid(), 1, PlanRevisionStatus.Active,
            October, October.AddDays(-1), null, Now));
        Assert.Throws<ArgumentException>(() => DietPlanRevision.Create(Guid.NewGuid(), 1, PlanRevisionStatus.Active,
            October, October.AddDays(PlanRevision.MaximumProgramDays + 1), null, Now));

        var oneDay = DietPlanRevision.Create(Guid.NewGuid(), 1, PlanRevisionStatus.Active, October, October, null, Now);
        Assert.True(oneDay.Covers(October));
        Assert.False(oneDay.Covers(October.AddDays(1)));
    }

    [Fact]
    public void A_version_cannot_be_created_or_saved_as_archived()
    {
        Assert.Throws<ArgumentException>(() => DietPlanRevision.Create(Guid.NewGuid(), 1, PlanRevisionStatus.Archived,
            null, null, null, Now));
        var revision = DietPlanRevision.Create(Guid.NewGuid(), 1, PlanRevisionStatus.Active, null, null, null, Now);
        Assert.Throws<ArgumentException>(() => revision.Update(PlanRevisionStatus.Archived, null, null, Now));
    }

    [Fact]
    public void Save_in_place_updates_dates_and_status_of_the_current_version()
    {
        var revision = WorkoutPlanRevision.Create(Guid.NewGuid(), 1, PlanRevisionStatus.Draft, null, null, null, Now);

        revision.Update(PlanRevisionStatus.Active, October, null, Now.AddHours(1));

        Assert.Equal(PlanRevisionStatus.Active, revision.Status);
        Assert.Equal(October, revision.StartDate);
        Assert.Null(revision.EndDate);
        Assert.Equal(Now.AddHours(1), revision.UpdatedAt);
        Assert.Equal(Now, revision.CreatedAt);
        Assert.True(revision.Covers(October.AddYears(5)));
        Assert.False(revision.Covers(October.AddDays(-1)));
    }

    [Fact]
    public void Archived_version_is_read_only()
    {
        var revision = DietPlanRevision.Create(Guid.NewGuid(), 1, PlanRevisionStatus.Active, October, null, null, Now);

        revision.Archive(Now.AddDays(3));

        Assert.True(revision.IsArchived);
        Assert.Equal(Now.AddDays(3), revision.ArchivedAt);
        Assert.Throws<InvalidOperationException>(() => revision.Update(PlanRevisionStatus.Active, October, null, Now));
        Assert.Throws<InvalidOperationException>(() => revision.Touch(Now));
        Assert.Throws<InvalidOperationException>(() => revision.Archive(Now));
        Assert.Equal(October, revision.StartDate);
    }

    [Fact]
    public void Delivery_names_its_version_and_only_diets_name_a_variant()
    {
        var delivery = DeliveredPlan.Create(Guid.NewGuid(), Guid.NewGuid(), DeliveredPlanType.Diet, Guid.NewGuid(),
            Guid.NewGuid(), 3, Guid.NewGuid(), "Plan", "en", "{}", DeliveryChannel.Download, null, Now);
        Assert.Equal(3, delivery.RevisionNumber);

        Assert.Throws<ArgumentException>(() => DeliveredPlan.Create(Guid.NewGuid(), Guid.NewGuid(), DeliveredPlanType.Diet,
            Guid.NewGuid(), Guid.NewGuid(), 1, null, "Plan", "en", "{}", DeliveryChannel.Download, null, Now));
        Assert.Throws<ArgumentException>(() => DeliveredPlan.Create(Guid.NewGuid(), Guid.NewGuid(), DeliveredPlanType.Workout,
            Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "Plan", "en", "{}", DeliveryChannel.Download, null, Now));
        Assert.Throws<ArgumentException>(() => DeliveredPlan.Create(Guid.NewGuid(), Guid.NewGuid(), DeliveredPlanType.Workout,
            Guid.NewGuid(), Guid.Empty, 1, null, "Plan", "en", "{}", DeliveryChannel.Download, null, Now));
    }

    [Fact]
    public void Every_content_row_names_its_version()
    {
        var revisionId = Guid.NewGuid();
        IPlanRevisionContent[] rows =
        [
            DietPlanNote.Create(Guid.NewGuid(), revisionId, "Water", 0, true),
            DietReplacementGroup.Create(Guid.NewGuid(), revisionId, Guid.NewGuid(), Guid.NewGuid(), null, "Swap", 0),
            MealFoodItem.Create(Guid.NewGuid(), revisionId, Guid.NewGuid(), Guid.NewGuid(), 50, 0, null),
            WorkoutPlanNote.Create(Guid.NewGuid(), revisionId, "Warm up", 0, true)
        ];
        Assert.All(rows, row => Assert.Equal(revisionId, row.RevisionId));
        Assert.Throws<ArgumentException>(() => DietPlanNote.Create(Guid.NewGuid(), Guid.Empty, "Water", 0, true));
    }
}
