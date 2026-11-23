using CoachHub.API.Common.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoachHub.IntegrationTests.Security;

/// <summary>P23: the API needs ASP.NET Core 10.0.13 or later (CVE-2026-70159); staging and production refuse older.</summary>
public sealed class RuntimeRequirementTests
{
    [Theory]
    [InlineData("10.0.15+3f1a2b", "10.0.15")]
    [InlineData("10.0.13-servicing.25", "10.0.13")]
    [InlineData("10.0.12", "10.0.12")]
    [InlineData("not a version", "0.0.0")]
    public void Versions_are_read_from_the_informational_version(string text, string expected) =>
        Assert.Equal(Version.Parse(expected), RuntimeRequirement.Parse(text));

    [Fact]
    public void This_test_run_uses_a_supported_runtime() => Assert.True(RuntimeRequirement.IsSupported(RuntimeRequirement.Current),
        $"ASP.NET Core {RuntimeRequirement.Current} is older than {RuntimeRequirement.Minimum}.");

    [Fact]
    public void Production_refuses_an_old_runtime_and_development_only_warns()
    {
        var old = new Version(10, 0, 12);
        Assert.Throws<InvalidOperationException>(() => RuntimeRequirement.Enforce(new Environment("Production"), NullLogger.Instance, old));
        Assert.Throws<InvalidOperationException>(() => RuntimeRequirement.Enforce(new Environment("Staging"), NullLogger.Instance, old));
        RuntimeRequirement.Enforce(new Environment("Development"), NullLogger.Instance, old);
        RuntimeRequirement.Enforce(new Environment("Testing"), NullLogger.Instance, old);
        RuntimeRequirement.Enforce(new Environment("Production"), NullLogger.Instance, new Version(10, 0, 13));
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "CoachHub.API";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
