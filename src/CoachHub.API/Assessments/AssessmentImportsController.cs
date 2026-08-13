using CoachHub.Application.Assessments.Importing;
using CoachHub.Application.Auth;
using CoachHub.Infrastructure.Assessments.Importing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Assessments;

[ApiController]
[Authorize(Roles = AuthRoles.Administrator)]
[Route("api/assessment-imports/profiles")]
public sealed class AssessmentImportsController(AssessmentImportService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ImportProfileResponse>> CreateProfile(
        SaveImportProfileInput input, CancellationToken token)
    {
        var profile = await service.CreateProfileAsync(input, token);
        return CreatedAtAction(nameof(UpdateProfile), new { id = profile.Id }, profile);
    }

    [HttpPut("{id:guid}")]
    public Task<ImportProfileResponse> UpdateProfile(
        Guid id, SaveImportProfileInput input, CancellationToken token) =>
        service.UpdateProfileAsync(id, input, token);

    [HttpPost("{id:guid}/imports")]
    [RequestSizeLimit(XlsxAssessmentWorkbookParser.MaximumFileSizeBytes)]
    public async Task<AssessmentImportSummary> Import(
        Guid id, [FromForm] IFormFile file, CancellationToken token)
    {
        await using var stream = file.OpenReadStream();
        return await service.ImportAsync(id, stream, token);
    }
}