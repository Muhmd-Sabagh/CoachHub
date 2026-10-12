using CoachHub.Domain.Settings;

namespace CoachHub.Domain.Tests.Settings;

public sealed class BrandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Colors_are_normalized_hex_and_blank_text_is_dropped()
    {
        var brand = Brand.Create(Guid.NewGuid(), " Coach A ", Now);

        brand.Update("Coach A", "#AABBCC", " ", "  ", "Welcome back", null, null, null, Now);

        Assert.Equal("Coach A", brand.BrandName);
        Assert.Equal("#aabbcc", brand.PrimaryColor);
        Assert.Null(brand.SecondaryColor);
        Assert.Null(brand.LoginHeadline);
        Assert.Equal("Welcome back", brand.LoginMessage);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#abc")]
    [InlineData("#gggggg")]
    [InlineData("javascript:alert(1)")]
    public void Invalid_colors_are_rejected(string color)
    {
        var brand = Brand.Create(null, "Platform", Now);
        Assert.Throws<ArgumentException>(() => brand.Update("Platform", color, null, null, null, null, null, null, Now));
    }

    [Fact]
    public void Coach_brands_take_valid_hosts_in_lower_case()
    {
        var brand = Brand.Create(Guid.NewGuid(), "Coach", Now);

        brand.SetHosts("Coach-A", "WWW.Coach-A.com.", Now);

        Assert.Equal("coach-a", brand.Subdomain);
        Assert.Equal("www.coach-a.com", brand.CustomDomain);
    }

    [Theory]
    [InlineData("-coach", null)]
    [InlineData("coach.a", null)]
    [InlineData("coach_a", null)]
    [InlineData(null, "localhost")]
    [InlineData(null, "coach a.com")]
    [InlineData(null, "https://coach.com")]
    public void Invalid_hosts_are_rejected(string? subdomain, string? customDomain)
    {
        var brand = Brand.Create(Guid.NewGuid(), "Coach", Now);
        Assert.Throws<ArgumentException>(() => brand.SetHosts(subdomain, customDomain, Now));
    }

    [Fact]
    public void The_platform_brand_has_no_hosts()
    {
        var brand = Brand.Create(null, "Platform", Now);
        Assert.Throws<ArgumentException>(() => brand.SetHosts("platform", null, Now));
    }

    [Fact]
    public void Images_are_set_and_cleared_per_kind()
    {
        var brand = Brand.Create(null, "Platform", Now);
        var logo = Guid.NewGuid();

        brand.SetImage(BrandImage.Logo, logo, Now);
        brand.SetImage(BrandImage.Favicon, Guid.NewGuid(), Now);
        brand.SetImage(BrandImage.Favicon, null, Now);

        Assert.Equal(logo, brand.ImageOf(BrandImage.Logo));
        Assert.Null(brand.ImageOf(BrandImage.Favicon));
        Assert.Throws<ArgumentException>(() => brand.SetImage(BrandImage.Logo, Guid.Empty, Now));
    }
}

public sealed class CoachSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Defaults_are_valid_and_map_each_activity_level()
    {
        var settings = CoachSettings.CreateDefault(Guid.NewGuid(), Now);

        Assert.Equal(1.2m, settings.FactorFor(ActivityLevel.Sedentary));
        Assert.Equal(1.9m, settings.FactorFor(ActivityLevel.ExtraActive));
        Assert.Equal(20m, settings.DeficitPercent);
        Assert.Equal(MacroMode.GramsPerKg, settings.DefaultMacroMode);
        Assert.Equal(100, new[] { settings.NutritionWeight, settings.WorkoutWeight, settings.CheckInWeight,
            settings.StepsWeight, settings.WaterWeight, settings.SleepWeight }.Sum());
    }

    [Fact]
    public void Adherence_weights_must_add_up_to_100()
    {
        var settings = CoachSettings.CreateDefault(Guid.NewGuid(), Now);
        var exception = Assert.Throws<ArgumentException>(() => Update(settings, weights: new AdherenceWeights(50, 50, 10, 0, 0, 0)));
        Assert.Equal("adherenceWeights", exception.ParamName);
    }

    [Fact]
    public void Activity_factors_must_be_in_range_and_not_decrease()
    {
        var settings = CoachSettings.CreateDefault(Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(() => Update(settings, factors: new ActivityFactors(0.9m, 1.3m, 1.5m, 1.7m, 1.9m)));
        Assert.Throws<ArgumentException>(() => Update(settings, factors: new ActivityFactors(1.2m, 1.6m, 1.5m, 1.7m, 1.9m)));
    }

    [Theory]
    [InlineData(-1, 10, 2, 0.8)]
    [InlineData(20, 60, 2, 0.8)]
    [InlineData(20, 10, 6, 0.8)]
    [InlineData(20, 10, 2, 4)]
    public void Percentages_and_grams_per_kg_are_bounded(double deficit, double surplus, double protein, double fat)
    {
        var settings = CoachSettings.CreateDefault(Guid.NewGuid(), Now);
        Assert.Throws<ArgumentException>(() => settings.Update(settings.Factors, (decimal)deficit, (decimal)surplus,
            (decimal)protein, (decimal)fat, MacroMode.Percentages, settings.Weights, FoodReplacementRule.Calories, Now));
    }

    [Fact]
    public void A_valid_update_replaces_every_field()
    {
        var settings = CoachSettings.CreateDefault(Guid.NewGuid(), Now);

        settings.Update(new ActivityFactors(1.1m, 1.3m, 1.5m, 1.7m, 2.0m), 15m, 12m, 2.2m, 1m, MacroMode.FixedGrams,
            new AdherenceWeights(40, 30, 10, 10, 5, 5), FoodReplacementRule.DominantMacro, Now.AddDays(1));

        Assert.Equal(1.1m, settings.SedentaryFactor);
        Assert.Equal(15m, settings.DeficitPercent);
        Assert.Equal(MacroMode.FixedGrams, settings.DefaultMacroMode);
        Assert.Equal(40, settings.NutritionWeight);
        Assert.Equal(FoodReplacementRule.DominantMacro, settings.ReplacementRule);
        Assert.Equal(Now.AddDays(1), settings.UpdatedAt);
    }

    private static void Update(CoachSettings settings, ActivityFactors? factors = null, AdherenceWeights? weights = null) =>
        settings.Update(factors ?? settings.Factors, 20m, 10m, 2m, 0.8m, MacroMode.GramsPerKg,
            weights ?? settings.Weights, FoodReplacementRule.Calories, Now);
}

public sealed class PlatformSettingsTests
{
    [Fact]
    public void Defaults_are_english_metric_with_notifications_on()
    {
        var settings = PlatformSettings.CreateDefault(DateTimeOffset.UtcNow);

        Assert.Equal(PlatformSettings.SingletonId, settings.Id);
        Assert.Equal("en", settings.DefaultLanguage);
        Assert.Equal(UnitSystem.Metric, settings.Units);
        Assert.True(settings.EmailNotificationsEnabled);
    }

    [Theory]
    [InlineData("fr", null)]
    [InlineData("en", "Coach <admin@example.com>")]
    [InlineData("en", "Line\nbreak")]
    public void Invalid_language_or_sender_name_is_rejected(string language, string? sender)
    {
        var settings = PlatformSettings.CreateDefault(DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => settings.Update(language, UnitSystem.Metric, true, true, sender, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_valid_update_is_applied()
    {
        var settings = PlatformSettings.CreateDefault(DateTimeOffset.UtcNow);

        settings.Update("AR", UnitSystem.Imperial, false, true, " CoachHub Team ", DateTimeOffset.UtcNow);

        Assert.Equal("ar", settings.DefaultLanguage);
        Assert.Equal(UnitSystem.Imperial, settings.Units);
        Assert.False(settings.EmailNotificationsEnabled);
        Assert.Equal("CoachHub Team", settings.EmailSenderName);
    }
}
