using System.Reflection;
using CoachHub.API.Common.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CoachHub.IntegrationTests.Security;

public sealed class ApiRouteVersioningTests
{
    [Fact]
    public void Every_controller_action_is_served_under_the_versioned_api_prefix()
    {
        var unversioned = new List<string>();

        foreach (var controller in typeof(Program).Assembly.GetTypes()
                     .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type)))
        {
            var controllerTemplates = controller.GetCustomAttributes<RouteAttribute>(inherit: true)
                .Select(route => route.Template)
                .DefaultIfEmpty(string.Empty)
                .ToArray();

            var actions = controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());

            foreach (var action in actions)
            {
                var actionTemplates = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                    .Select(attribute => attribute.Template);

                foreach (var actionTemplate in actionTemplates)
                foreach (var controllerTemplate in controllerTemplates)
                {
                    var route = Combine(controllerTemplate, actionTemplate);
                    if (!route.StartsWith(ApiRoutes.V1 + "/", StringComparison.Ordinal))
                    {
                        unversioned.Add($"{controller.Name}.{action.Name} -> {route}");
                    }
                }
            }
        }

        Assert.True(
            unversioned.Count == 0,
            "Controller routes must start with '" + ApiRoutes.V1 + "/': " + string.Join(", ", unversioned));
    }

    private static string Combine(string controllerTemplate, string? actionTemplate)
    {
        if (string.IsNullOrEmpty(actionTemplate)) return controllerTemplate;
        if (actionTemplate.StartsWith('/') || actionTemplate.StartsWith("~/", StringComparison.Ordinal))
        {
            return actionTemplate.TrimStart('~', '/');
        }

        return string.IsNullOrEmpty(controllerTemplate)
            ? actionTemplate
            : controllerTemplate + "/" + actionTemplate;
    }
}
