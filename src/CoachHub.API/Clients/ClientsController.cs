using CoachHub.Application.Auth;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Clients;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
[Route("api/clients")]
public sealed class ClientsController(
    ClientService clientService,
    SubscriptionService subscriptionService) : ControllerBase
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

    [HttpPost("{id:guid}/form-code/regenerate")]
    public async Task<FormCodeResponse> RegenerateFormCode(
        Guid id,
        CancellationToken cancellationToken) =>
        new(await clientService.RegenerateFormCodeAsync(id, cancellationToken));

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
        return Created($"/api/clients/{clientId}/subscriptions/{created.Id}", created);
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

public sealed record FormCodeResponse(string FormCode);