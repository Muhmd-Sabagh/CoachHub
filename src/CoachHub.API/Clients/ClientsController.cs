using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.DietPlanning;
using CoachHub.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Clients;

[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients")]
public sealed class ClientsController(
    ClientService clientService,
    ClientProfileService profileService,
    SubscriptionService subscriptionService,
    NutritionTargetService nutritionTargets) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ClientResponse>> List(
        [FromQuery] ClientQuery query,
        CancellationToken cancellationToken) =>
        clientService.ListAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ClientDetailResponse> Get(Guid id, CancellationToken cancellationToken) =>
        clientService.GetAsync(id, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<ClientResponse>> Create(
        ClientCreateInput input,
        CancellationToken cancellationToken)
    {
        var created = await clientService.CreateAsync(input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public Task<ClientResponse> Update(
        Guid id,
        ClientUpdateInput input,
        CancellationToken cancellationToken) =>
        clientService.UpdateAsync(id, input, cancellationToken);

    /// <summary>Calorie and macro targets from the client's profile and macro settings (§14, §15).</summary>
    [HttpGet("{id:guid}/nutrition-targets")]
    public Task<EnergyCalculatorResponse> NutritionTargets(Guid id, CancellationToken cancellationToken) =>
        nutritionTargets.ForClientAsync(id, cancellationToken);

    /// <summary>The structured profile (§3), entered at onboarding and editable later.</summary>
    [HttpGet("{id:guid}/profile")]
    public Task<ClientProfileResponse> Profile(Guid id, CancellationToken cancellationToken) =>
        profileService.GetAsync(id, cancellationToken);

    [HttpPut("{id:guid}/profile")]
    public Task<ClientProfileResponse> UpdateProfile(Guid id, ClientProfileInput input, CancellationToken cancellationToken) =>
        profileService.UpdateAsync(id, input, cancellationToken);

    /// <summary>Creates the client's login if needed and emails a set-password invitation (again).</summary>
    [HttpPost("{id:guid}/invite")]
    public Task<ClientAccountResponse> Invite(Guid id, CancellationToken cancellationToken) =>
        clientService.InviteAsync(id, cancellationToken);

    /// <summary>Sets the client's status by hand (§39).</summary>
    [HttpPut("{id:guid}/status")]
    public Task<ClientResponse> SetStatus(Guid id, ClientStatusInput input, CancellationToken cancellationToken) =>
        clientService.SetStatusAsync(id, input, cancellationToken);

    /// <summary>Returns the client to their computed status.</summary>
    [HttpDelete("{id:guid}/status")]
    public Task<ClientResponse> ClearStatus(Guid id, CancellationToken cancellationToken) =>
        clientService.SetStatusAsync(id, null, cancellationToken);

    /// <summary>Moves the client to another coach (§56, D7).</summary>
    [HttpPost("{id:guid}/transfer")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<ClientResponse> Transfer(
        Guid id,
        ClientTransferInput input,
        CancellationToken cancellationToken) =>
        clientService.TransferAsync(id, input, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await clientService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{clientId:guid}/subscriptions")]
    public async Task<ActionResult<SubscriptionResponse>> CreateSubscription(
        Guid clientId,
        SubscriptionInput input,
        CancellationToken cancellationToken)
    {
        var created = await subscriptionService.CreateAsync(clientId, input, cancellationToken);
        return Created($"/{ApiRoutes.V1}/clients/{clientId}/subscriptions/{created.Id}", created);
    }

    [HttpPut("{clientId:guid}/subscriptions/{id:guid}")]
    public Task<SubscriptionResponse> UpdateSubscription(
        Guid clientId,
        Guid id,
        SubscriptionInput input,
        CancellationToken cancellationToken) =>
        subscriptionService.UpdateAsync(clientId, id, input, cancellationToken);

    [HttpPost("{clientId:guid}/subscriptions/{id:guid}/renewals")]
    public Task<SubscriptionResponse> RenewSubscription(
        Guid clientId,
        Guid id,
        SubscriptionRenewalInput input,
        CancellationToken cancellationToken) =>
        subscriptionService.RenewAsync(clientId, id, input, cancellationToken);

    [HttpDelete("{clientId:guid}/subscriptions/{id:guid}")]
    public async Task<IActionResult> DeleteSubscription(
        Guid clientId,
        Guid id,
        CancellationToken cancellationToken)
    {
        await subscriptionService.DeleteAsync(clientId, id, cancellationToken);
        return NoContent();
    }
}
