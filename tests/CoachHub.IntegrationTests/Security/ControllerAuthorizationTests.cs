using System.Reflection;
using CoachHub.API.Auth;
using CoachHub.API.Settings;
using CoachHub.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CoachHub.IntegrationTests.Security;

public sealed class ControllerAuthorizationTests
{
    private static readonly HashSet<string> ExpectedAnonymousActions =
    [
        $"{nameof(AuthController)}.{nameof(AuthController.Login)}",
        $"{nameof(AuthController)}.{nameof(AuthController.Refresh)}",
        // D5 "authentication endpoints": the login page is branded before sign-in (P4).
        $"{nameof(BrandingController)}.{nameof(BrandingController.Public)}",
        $"{nameof(BrandingController)}.{nameof(BrandingController.Asset)}",
        $"{nameof(AuthController)}.{nameof(AuthController.RequestReset)}",
        $"{nameof(AuthController)}.{nameof(AuthController.Reset)}"
        // D5 (P5): the code-based client forms are retired; clients submit signed in.
    ];

    [Fact]
    public void Every_controller_action_is_explicitly_authorized_or_an_approved_rate_limited_public_action()
    {
        var actualAnonymousActions = new HashSet<string>();

        foreach (var controller in ApiControllers())
        {
            foreach (var action in ControllerActions(controller))
            {
                var actionName = $"{controller.Name}.{action.Name}";
                var isAnonymous = HasAttribute<AllowAnonymousAttribute>(controller, action);

                if (isAnonymous)
                {
                    actualAnonymousActions.Add(actionName);
                    Assert.True(
                        HasAttribute<EnableRateLimitingAttribute>(controller, action),
                        $"Anonymous action {actionName} must be rate limited.");
                    continue;
                }

                Assert.NotEmpty(Attributes<AuthorizeAttribute>(controller, action));
            }
        }

        Assert.True(
            ExpectedAnonymousActions.SetEquals(actualAnonymousActions),
            "The approved anonymous API surface changed. Expected: " +
            string.Join(", ", ExpectedAnonymousActions.Order()) +
            "; actual: " + string.Join(", ", actualAnonymousActions.Order()));
    }

    [Fact]
    public void Every_authorization_attribute_names_a_known_role_policy()
    {
        string[] known = [AuthPolicies.AdminOnly, AuthPolicies.CoachOrAdmin, AuthPolicies.ClientOnly, AuthPolicies.AnyRole];
        var offending = new List<string>();

        foreach (var controller in ApiControllers())
        {
            var attributes = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                .Concat(controller.GetMethods().SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)));
            foreach (var attribute in attributes)
            {
                if (attribute.Roles is not null || (attribute.Policy is { } policy && !known.Contains(policy)))
                    offending.Add($"{controller.Name}: Policy={attribute.Policy}, Roles={attribute.Roles}");
            }
        }

        Assert.True(offending.Count == 0, "Use AuthPolicies role policies: " + string.Join("; ", offending));
    }

    [Fact]
    public void Public_branding_has_its_own_rate_limit_policy()
    {
        foreach (var action in new[] { nameof(BrandingController.Public), nameof(BrandingController.Asset) })
            Assert.Equal("branding", typeof(BrandingController).GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    public void Session_refresh_has_its_own_rate_limit_policy()
    {
        var refresh = typeof(AuthController).GetMethod(nameof(AuthController.Refresh))!;

        Assert.Equal("session-refresh", refresh.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    public void Authentication_uses_its_own_limit_policy_and_no_code_based_surface_remains()
    {
        var login = typeof(AuthController).GetMethod(nameof(AuthController.Login))!;

        Assert.Equal("authentication", login.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.DoesNotContain(typeof(Program).Assembly.GetTypes(), type => type.Name == "ClientFormsController");
        Assert.Equal(AuthPolicies.ClientOnly,
            typeof(CoachHub.API.Me.MyFormsController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
    }

    private static IEnumerable<Type> ApiControllers() => typeof(Program).Assembly
        .GetTypes()
        .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

    private static IEnumerable<MethodInfo> ControllerActions(Type controller) => controller
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

    private static bool HasAttribute<T>(Type controller, MethodInfo action)
        where T : Attribute => Attributes<T>(controller, action).Any();

    private static IEnumerable<T> Attributes<T>(Type controller, MethodInfo action)
        where T : Attribute => controller.GetCustomAttributes<T>(inherit: true)
            .Concat(action.GetCustomAttributes<T>(inherit: true));
}
