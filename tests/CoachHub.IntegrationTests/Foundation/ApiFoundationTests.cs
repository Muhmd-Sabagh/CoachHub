using CoachHub.API.Settings;

namespace CoachHub.IntegrationTests.Foundation;

public sealed class ApiFoundationTests
{
    [Fact]
    public void CoachHub_configuration_section_name_is_stable()
    {
        Assert.Equal("CoachHub", CoachHubOptions.SectionName);
    }
}
