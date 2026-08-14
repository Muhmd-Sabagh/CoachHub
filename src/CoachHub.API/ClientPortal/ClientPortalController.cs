using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoachHub.Application.Auth;
using CoachHub.Application.Billing;
using CoachHub.Application.Clients;
using CoachHub.Application.PlanDelivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.ClientPortal;

[ApiController]
[Route("api/client-portal")]
[Authorize(Roles = AuthRoles.Client)]
public sealed class ClientPortalController(ClientService clients, BillingService billing, PlanDeliveryService deliveries) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ClientPortalResponse> Overview(CancellationToken token)
    {
        var clientId = RequiredClientId();
        return new(await clients.GetAsync(clientId, token), await billing.ListAsync(clientId, token), await deliveries.ListAsync(clientId, token));
    }
    private Guid RequiredClientId() => Guid.TryParse(User.FindFirstValue("client_id"), out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("Client account is not linked.");
}
public sealed record ClientPortalResponse(ClientDetailResponse Client, IReadOnlyList<InvoiceResponse> Invoices, IReadOnlyList<DeliveredPlanResponse> DeliveredPlans);
