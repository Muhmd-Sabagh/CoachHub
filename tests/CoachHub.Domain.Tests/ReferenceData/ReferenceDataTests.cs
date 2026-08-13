using CoachHub.Domain.ReferenceData;

namespace CoachHub.Domain.Tests.ReferenceData;

public sealed class ReferenceDataTests
{
    [Fact]
    public void Package_requires_english_but_allows_missing_arabic_name()
    {
        var package = Package.Create("  Online Coaching  ", null, " Monthly support ");

        Assert.Equal("Online Coaching", package.NameEn);
        Assert.Null(package.NameAr);
        Assert.Equal("Monthly support", package.Description);
        Assert.True(package.IsActive);
    }

    [Fact]
    public void Currency_normalizes_its_business_code()
    {
        var currency = Currency.Create(" egp ", "Egyptian Pound", "E£");

        Assert.Equal("EGP", currency.Code);
    }

    [Fact]
    public void Reference_records_can_be_deactivated_without_deletion()
    {
        var category = FoodCategory.Create("Protein", "بروتين");

        category.SetActive(false);

        Assert.False(category.IsActive);
    }
}