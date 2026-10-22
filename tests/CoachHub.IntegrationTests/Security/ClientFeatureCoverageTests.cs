using System.Reflection;
using CoachHub.API.Auth;
using CoachHub.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.IntegrationTests.Security;

/// <summary>
/// P16 (§57): every client-area controller declares the feature the subscription access rules apply to, so a new
/// endpoint cannot bypass them. The allowlist holds the parts that must never be restricted.
/// </summary>
public sealed class ClientFeatureCoverageTests
{
    private static readonly HashSet<string> NeverRestricted =
    [
        // What the client may do now; the app needs it to explain restrictions.
        "MyAccessController",
        // The subscription and invoices themselves, which the client must see to renew.
        "MyHomeController",
    ];

    [Fact]
    public void Every_client_controller_declares_its_feature()
    {
        var controllers = typeof(ClientFeatureAttribute).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => type.GetCustomAttributes<AuthorizeAttribute>().Any(x => x.Policy == AuthPolicies.ClientOnly))
            .ToArray();
        Assert.NotEmpty(controllers);
        var missing = controllers
            .Where(type => type.GetCustomAttribute<ClientFeatureAttribute>() is null && !NeverRestricted.Contains(type.Name))
            .Select(type => type.Name)
            .ToArray();
        Assert.True(missing.Length == 0, "Add [ClientFeature(...)] (or allowlist with a reason): " + string.Join(", ", missing));
    }
}
