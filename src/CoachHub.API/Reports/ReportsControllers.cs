using System.Security.Claims;
using CoachHub.API.Auth;
using CoachHub.API.Common.Routing;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Exports;
using CoachHub.Application.Pdf;
using CoachHub.Application.Reports;
using CoachHub.Domain.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Reports;

/// <summary>A client's weekly or monthly report (§44) as the coach sees it, and as PDF. Another coach's client is not found.</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/clients/{clientId:guid}/reports")]
public sealed class ClientReportsController(ReportService reports) : ControllerBase
{
    [HttpGet]
    public Task<ClientReport> Get(Guid clientId, [FromQuery] ReportQuery query, CancellationToken token) => reports.GetAsync(clientId, query, token);

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(Guid clientId, [FromQuery] ReportQuery query, CancellationToken token)
    {
        var pdf = await reports.PdfAsync(clientId, query, token);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }
}

/// <summary>The signed-in client's own reports (§44); part of Progress for the subscription rules (§57).</summary>
[ApiController]
[Authorize(Policy = AuthPolicies.ClientOnly)]
[ClientFeature(ClientFeature.Progress)]
[Route(ApiRoutes.V1 + "/me/reports")]
public sealed class MyReportsController(ReportService reports) : ControllerBase
{
    [HttpGet]
    public Task<ClientReport> Get([FromQuery] ReportQuery query, CancellationToken token) => reports.GetAsync(ClientId(), query, token);

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf([FromQuery] ReportQuery query, CancellationToken token)
    {
        var pdf = await reports.PdfAsync(ClientId(), query, token);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    private Guid ClientId() =>
        Guid.TryParse(User.FindFirstValue(AuthClaims.ClientId), out var id) && id != Guid.Empty
            ? id
            : throw new ForbiddenException("The account is not linked to a client.");
}

/// <summary>
/// Data export (§51) as CSV: the coach's client list and, per client, one dataset or all in a ZIP; the platform
/// exports are the Administrator's. Every export is recorded in the audit trail.
/// </summary>
[ApiController]
[Authorize(Policy = AuthPolicies.CoachOrAdmin)]
[Route(ApiRoutes.V1 + "/exports")]
public sealed class ExportsController(ExportService exports) : ControllerBase
{
    [HttpGet("clients")]
    public async Task<IActionResult> Clients(CancellationToken token) => Send(await exports.ClientListAsync(token));

    /// <summary>Client, Progress, Nutrition, Workouts, CheckIns (CSV) or All (ZIP).</summary>
    [HttpGet("clients/{clientId:guid}/{dataset}")]
    public async Task<IActionResult> Client(Guid clientId, ClientExport dataset, CancellationToken token) =>
        Send(await exports.ClientAsync(clientId, dataset, token));

    /// <summary>Coaches, Clients, Subscriptions, or the Revenue ledger for a period.</summary>
    [HttpGet("platform/{dataset}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> Platform(PlatformExport dataset, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken token) =>
        Send(await exports.PlatformAsync(dataset, from, to, token));

    private FileContentResult Send(ExportFile file) => File(file.Content, file.ContentType, file.FileName);
}
