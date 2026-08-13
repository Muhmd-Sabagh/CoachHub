using System.Reflection;
using CoachHub.API.Assessments;
using CoachHub.API.Auth;
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
        $"{nameof(ClientFormsController)}.{nameof(ClientFormsController.Validate)}",
        $"{nameof(ClientFormsController)}.{nameof(ClientFormsController.Questions)}",
        $"{nameof(ClientFormsController)}.{nameof(ClientFormsController.Submit)}",
        $"{nameof(ClientFormsController)}.{nameof(ClientFormsController.UploadMedia)}"
    ];

    [Fact]
    public void Every_controller_action_is_explicitly_admin_only_or_an_approved_rate_limited_public_action()
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

                var roles = Attributes<AuthorizeAttribute>(controller, action)
                    .SelectMany(attribute => (attribute.Roles ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .ToHashSet(StringComparer.Ordinal);

                Assert.Contains(AuthRoles.Administrator, roles);
            }
        }

        Assert.True(
            ExpectedAnonymousActions.SetEquals(actualAnonymousActions),
            "The approved anonymous API surface changed. Expected: " +
            string.Join(", ", ExpectedAnonymousActions.Order()) +
            "; actual: " + string.Join(", ", actualAnonymousActions.Order()));
    }

    [Fact]
    public void Authentication_and_client_form_public_surfaces_use_separate_limit_policies()
    {
        var login = typeof(AuthController).GetMethod(nameof(AuthController.Login))!;
        var validate = typeof(ClientFormsController).GetMethod(nameof(ClientFormsController.Validate))!;

        Assert.Equal(
            "authentication",
            login.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.Equal(
            "client-forms",
            typeof(ClientFormsController).GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.NotNull(validate);
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
