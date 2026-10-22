using System.Security.Claims;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Billing;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.PlanDelivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Me;

/// <summary>
/// The client area's home: the client's record with their subscriptions, their invoices and the plans delivered to
/// them. Never restricted, so a client with an expired subscription can still see what to renew (§57).
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[Route(ApiRoutes.V1 + "/me/home")]
public sealed class MyHomeController(ClientService clients, BillingService billing, PlanDeliveryService deliveries) : ControllerBase
{
    [HttpGet]
    public async Task<MyHomeResponse> Get(CancellationToken token)
    {
        var clientId = Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
        return new(await clients.GetAsync(clientId, token), await billing.ListAsync(clientId, token), await deliveries.ListAsync(clientId, token));
    }
}

public sealed record MyHomeResponse(ClientDetailResponse Client, IReadOnlyList<InvoiceResponse> Invoices, IReadOnlyList<DeliveredPlanResponse> DeliveredPlans);
