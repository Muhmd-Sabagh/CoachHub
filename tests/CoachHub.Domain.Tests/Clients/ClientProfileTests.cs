using CoachHub.Domain.Clients;
using CoachHub.Domain.Settings;

namespace CoachHub.Domain.Tests.Clients;

public sealed class ClientProfileTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static ClientProfileValues Values(
        DateOnly? dateOfBirth = null,
        decimal? heightCm = 175m,
        int? trainingDays = 4,
        Guid[]? preferred = null,
        Guid[]? disliked = null,
        Guid[]? equipment = null,
        DietaryRestrictions restrictions = DietaryRestrictions.None) => new(
        dateOfBirth ?? new DateOnly(1990, 10, 13), Sex.Female, heightCm, 70m, 22m, FitnessGoal.LoseFat,
        ActivityLevel.ModeratelyActive, trainingDays, 8000, TrainingLocation.Gym, "Likes mornings", "Mediterranean",
        preferred ?? [], "Oats", disliked ?? [], null, restrictions, "No pork", equipment ?? [], 4,
        new TimeOnly(23, 0), new TimeOnly(7, 0), "Desk job");

    [Fact]
    public void Age_is_derived_from_the_date_of_birth()
    {
        var profile = ClientProfile.Create(Guid.NewGuid(), Now);
        profile.Update(Values(new DateOnly(1990, 10, 13)), Today, Now);

        Assert.Equal(35, profile.AgeOn(Today));
        Assert.Equal(36, profile.AgeOn(new DateOnly(2026, 10, 13)));
    }

    [Fact]
    public void Typed_values_and_notes_are_stored()
    {
        var profile = ClientProfile.Create(Guid.NewGuid(), Now);
        profile.Update(Values(restrictions: DietaryRestrictions.Halal | DietaryRestrictions.LactoseFree), Today, Now);

        Assert.Equal(Sex.Female, profile.Sex);
        Assert.Equal(175m, profile.HeightCm);
        Assert.Equal(ActivityLevel.ModeratelyActive, profile.ActivityLevel);
        Assert.True(profile.DietaryRestrictions.HasFlag(DietaryRestrictions.Halal));
        Assert.Equal(new TimeOnly(23, 0), profile.Bedtime);
        Assert.Equal("No pork", profile.DietaryNotes);
    }

    [Theory]
    [InlineData(40, 4)]
    [InlineData(175, 8)]
    public void Out_of_range_measurements_are_rejected(int height, int trainingDays)
    {
        var profile = ClientProfile.Create(Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(() => profile.Update(Values(heightCm: height, trainingDays: trainingDays), Today, Now));
    }

    [Fact]
    public void A_client_must_be_at_least_ten_years_old()
    {
        var profile = ClientProfile.Create(Guid.NewGuid(), Now);
        var exception = Assert.Throws<ArgumentException>(() => profile.Update(Values(Today.AddYears(-5)), Today, Now));
        Assert.Equal("dateOfBirth", exception.ParamName);
    }

    [Fact]
    public void A_food_cannot_be_both_preferred_and_disliked()
    {
        var food = Guid.NewGuid();
        var profile = ClientProfile.Create(Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(() => profile.Update(Values(preferred: [food], disliked: [food]), Today, Now));
    }

    [Fact]
    public void Equipment_and_food_choices_are_replaced_without_duplicates()
    {
        var (barbell, bands, oats, fish) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var profile = ClientProfile.Create(Guid.NewGuid(), Now);

        profile.Update(Values(equipment: [barbell, barbell, bands], preferred: [oats], disliked: [fish]), Today, Now);
        profile.Update(Values(equipment: [bands], preferred: [fish], disliked: []), Today, Now);

        Assert.Equal(bands, Assert.Single(profile.Equipment).EquipmentId);
        var food = Assert.Single(profile.Foods);
        Assert.Equal((fish, FoodPreferenceKind.Preferred), (food.FoodItemId, food.Kind));
    }

    [Fact]
    public void Unknown_dietary_flags_are_rejected()
    {
        var profile = ClientProfile.Create(Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(() => profile.Update(Values(restrictions: (DietaryRestrictions)4096), Today, Now));
    }
}

public sealed class ClientStatusTests
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static Client NewClient() => Client.Create(Guid.NewGuid(), "Client", null, null, Today);

    [Fact]
    public void A_new_active_client_is_on_track()
    {
        Assert.Equal(ClientStatus.OnTrack, NewClient().GetStatus(Today));
    }

    [Fact]
    public void Inactive_wins_over_review_and_review_follows_the_check_in()
    {
        var client = NewClient();
        client.SetCheckInState(null, true);
        Assert.Equal(ClientStatus.NeedsReview, client.GetStatus(Today));

        client.Update("Client", null, null, false);
        Assert.Equal(ClientStatus.Inactive, client.GetStatus(Today));
    }

    [Fact]
    public void A_manual_status_lasts_until_its_end_date_and_can_be_cleared()
    {
        var client = NewClient();
        client.OverrideStatus(ClientStatus.CheckInOverdue, Today.AddDays(2), Today);

        Assert.Equal(ClientStatus.CheckInOverdue, client.GetStatus(Today.AddDays(2)));
        Assert.Equal(ClientStatus.OnTrack, client.GetStatus(Today.AddDays(3)));

        client.OverrideStatus(ClientStatus.NeedsReview, null, Today);
        Assert.Equal(ClientStatus.NeedsReview, client.GetStatus(Today.AddYears(2)));
        client.ClearStatusOverride();
        Assert.Equal(ClientStatus.OnTrack, client.GetStatus(Today));
    }

    [Fact]
    public void An_override_must_end_between_today_and_a_year_ahead()
    {
        var client = NewClient();
        Assert.Throws<ArgumentException>(() => client.OverrideStatus(ClientStatus.OnTrack, Today.AddDays(-1), Today));
        Assert.Throws<ArgumentException>(() => client.OverrideStatus(ClientStatus.OnTrack, Today.AddYears(2), Today));
    }
}
