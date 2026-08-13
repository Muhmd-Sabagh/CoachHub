using CoachHub.Application.Assessments;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Assessments;

[ApiController, Authorize(Roles = AuthRoles.Administrator), Route("api/assessment-submissions")]
public sealed class AssessmentSubmissionsController(AssessmentAdminQueryService service) : ControllerBase
{
    [HttpGet] public Task<PagedResult<AssessmentSubmissionSummary>> List([FromQuery] AssessmentSubmissionQuery query, CancellationToken token) => service.ListSubmissionsAsync(query, token);
    [HttpGet("{id:guid}")] public Task<AssessmentSubmissionDetail> Get(Guid id, CancellationToken token) => service.GetSubmissionAsync(id, token);
}