using System.Net.Mime;
using CoachHub.Application.Auth;
using CoachHub.Application.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace CoachHub.API.Pdf;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
public sealed class PlanPdfController(PlanPdfService service) : ControllerBase
{
    [HttpGet("api/diet-plans/{id:guid}/pdf/preview")]
    public async Task<IActionResult> PreviewDiet(
        Guid id, [FromQuery] PlanPdfLanguage language = PlanPdfLanguage.English,
        CancellationToken cancellationToken = default) =>
        Inline(await service.DietAsync(id, language, cancellationToken));

    [HttpGet("api/diet-plans/{id:guid}/pdf/download")]
    public async Task<IActionResult> DownloadDiet(
        Guid id, [FromQuery] PlanPdfLanguage language = PlanPdfLanguage.English,
        CancellationToken cancellationToken = default) =>
        Download(await service.DietAsync(id, language, cancellationToken));

    [HttpGet("api/workout-plans/{id:guid}/pdf/preview")]
    public async Task<IActionResult> PreviewWorkout(
        Guid id, [FromQuery] PlanPdfLanguage language = PlanPdfLanguage.English,
        CancellationToken cancellationToken = default) =>
        Inline(await service.WorkoutAsync(id, language, cancellationToken));

    [HttpGet("api/workout-plans/{id:guid}/pdf/download")]
    public async Task<IActionResult> DownloadWorkout(
        Guid id, [FromQuery] PlanPdfLanguage language = PlanPdfLanguage.English,
        CancellationToken cancellationToken = default) =>
        Download(await service.WorkoutAsync(id, language, cancellationToken));

    private IActionResult Inline(GeneratedPlanPdf document)
    {
        Response.Headers[HeaderNames.ContentDisposition] =
            new ContentDisposition { Inline = true, FileName = document.FileName }.ToString();
        return File(document.Content, MediaTypeNames.Application.Pdf, enableRangeProcessing: true);
    }

    private IActionResult Download(GeneratedPlanPdf document) =>
        File(document.Content, MediaTypeNames.Application.Pdf, document.FileName, enableRangeProcessing: true);
}
