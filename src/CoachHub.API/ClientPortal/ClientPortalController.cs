using CoachHub.API.Common.Routing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.Application.Auth;
using CoachHub.Application.Billing;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.PlanDelivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.ClientPortal;

[ApiController]
[Route(ApiRoutes.V1 + "/client-portal")]
[Authorize(Policy = AuthPolicies.ClientOnly)]
public sealed class ClientPortalController(ClientService clients, BillingService billing, PlanDeliveryService deliveries) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ClientPortalResponse> Overview(CancellationToken token)
    {
        var clientId = RequiredClientId();
        return new(await clients.GetAsync(clientId, token), await billing.ListAsync(clientId, token), await deliveries.ListAsync(clientId, token));
    }
    private Guid RequiredClientId() => Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty ? id : throw new ForbiddenException("Client account is not linked.");
}
public sealed record ClientPortalResponse(ClientDetailResponse Client, IReadOnlyList<InvoiceResponse> Invoices, IReadOnlyList<DeliveredPlanResponse> DeliveredPlans);
