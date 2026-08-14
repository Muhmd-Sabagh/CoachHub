using CoachHub.Application.Auth;
using CoachHub.Application.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Billing;

[ApiController]
[Route("api/billing/invoices")]
[Authorize(Policy = AuthPermissions.ManageBilling)]
public sealed class BillingController(BillingService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<InvoiceResponse>> List([FromQuery] Guid? clientId, CancellationToken token) => service.ListAsync(clientId, token);
    [HttpPost] public async Task<ActionResult<InvoiceResponse>> Create(InvoiceInput input, CancellationToken token) { var created = await service.CreateAsync(input, token); return Created($"/api/billing/invoices/{created.Id}", created); }
    [HttpPost("{id:guid}/payments")] public Task<InvoiceResponse> Pay(Guid id, PaymentInput input, CancellationToken token) => service.PayAsync(id, input, token);
    [HttpPost("payments/{paymentId:guid}/refunds")] public Task<InvoiceResponse> Refund(Guid paymentId, RefundInput input, CancellationToken token) => service.RefundAsync(paymentId, input, token);
    [HttpPost("{id:guid}/void")] public async Task<IActionResult> Void(Guid id, CancellationToken token) { await service.VoidAsync(id, token); return NoContent(); }
}
