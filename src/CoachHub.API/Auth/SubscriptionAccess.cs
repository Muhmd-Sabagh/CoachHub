using System.Security.Claims;
using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Localization;
using CoachHub.Domain.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Auth;

/// <summary>Declares which client feature an endpoint belongs to, for the subscription access rules (§57).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ClientFeatureAttribute(ClientFeature feature) : Attribute
{
    public ClientFeature Feature { get; } = feature;
}

/// <summary>Part of the <see cref="AuthPolicies.ClientOnly"/> policy: the endpoint's feature must be open to the client.</summary>
public sealed class SubscriptionAccessRequirement : IAuthorizationRequirement;

/// <summary>
/// Applies the Administrator's access rules (§57) to client endpoints that declare a <see cref="ClientFeatureAttribute"/>:
/// reading (GET, HEAD) needs Full or Read-only access, anything else needs Full. Endpoints without a feature, such as
/// the account, notifications and the access summary, are never restricted.
/// </summary>
public sealed class SubscriptionAccessHandler : AuthorizationHandler<SubscriptionAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SubscriptionAccessRequirement requirement)
    {
        if (context.Resource is not HttpContext http ||
            http.GetEndpoint()?.Metadata.GetMetadata<ClientFeatureAttribute>() is not { } feature ||
            !Guid.TryParse(context.User.FindFirstValue(AuthClaims.ClientId), out var clientId))
        {
            context.Succeed(requirement);
            return;
        }
        var write = !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method);
        var decision = await http.RequestServices.GetRequiredService<SubscriptionAccessService>()
            .CheckAsync(clientId, feature.Feature, write, http.RequestAborted);
        if (decision.Allowed) context.Succeed(requirement);
        else context.Fail(new SubscriptionAccessFailure(this, decision.Message!, decision.Code!));
    }
}

/// <summary>A refusal by the access rules: its message is in the request's language, and its catalog code (P19).</summary>
public sealed class SubscriptionAccessFailure(IAuthorizationHandler handler, string message, string code)
    : AuthorizationFailureReason(handler, message)
{
    public string Code { get; } = code;
}

/// <summary>Answers a request refused by the access rules with a 403 that says why, so the client can show it.</summary>
public sealed class SubscriptionAccessResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var reason = authorizeResult.AuthorizationFailure?.FailureReasons.OfType<SubscriptionAccessFailure>().FirstOrDefault();
        if (authorizeResult.Forbidden && reason is not null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = Messages.Text("problem.subscriptionAccess", UiCulture.Current),
                Detail = reason.Message,
                Type = "https://coachhub/problems/subscription-access",
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = reason.Code;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
            return;
        }
        await fallback.HandleAsync(next, context, policy, authorizeResult);
    }
}
