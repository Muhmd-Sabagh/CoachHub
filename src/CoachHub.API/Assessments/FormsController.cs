using CoachHub.Application.Assessments;
using CoachHub.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Assessments;

[ApiController]
[Authorize(Policy = AuthPermissions.ManageAssessments)]
[Route("api/assessment-forms")]
public sealed class FormsController(FormAdminService service, AssessmentAdminQueryService queries) : ControllerBase
{
    [HttpGet]
    public Task<CoachHub.Application.Common.Models.PagedResult<FormSummary>> List([FromQuery] FormAdminQuery query, CancellationToken token) => queries.ListFormsAsync(query, token);

    [HttpPost]
    public async Task<ActionResult<FormVersionResponse>> Create(CreateFormInput input, CancellationToken token)
    {
        var result = await service.CreateAsync(input, token);
        return CreatedAtAction(nameof(Preview), new { id = result.DefinitionId }, result);
    }
    [HttpPut("{id:guid}")]
    public Task<FormSummary> Update(Guid id, UpdateFormInput input, CancellationToken token) =>
        service.UpdateAsync(id, input.Name, input.IsArchived, token);
    [HttpGet("{id:guid}/preview")]
    public Task<FormVersionResponse> Preview(Guid id, CancellationToken token) => service.PreviewAsync(id, token);
    [HttpPost("{id:guid}/sections")]
    public Task<SectionResponse> AddSection(Guid id, SectionInput input, CancellationToken token) =>
        service.AddSectionAsync(id, input, token);
    [HttpPost("{id:guid}/questions")]
    public Task<QuestionResponse> AddQuestion(Guid id, QuestionInput input, CancellationToken token) =>
        service.AddQuestionAsync(id, input, token);
    [HttpPut("{id:guid}/questions/{questionId:guid}")]
    public Task<QuestionResponse> UpdateQuestion(
        Guid id, Guid questionId, QuestionInput input, CancellationToken token) =>
        service.UpdateQuestionAsync(id, questionId, input, token);
    [HttpDelete("{id:guid}/questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid id, Guid questionId, CancellationToken token)
    { await service.DeleteQuestionAsync(id, questionId, token); return NoContent(); }
    [HttpPut("{id:guid}/questions/order")]
    public async Task<IActionResult> Reorder(Guid id, ReorderQuestionsInput input, CancellationToken token)
    { await service.ReorderAsync(id, input, token); return NoContent(); }
    [HttpPost("{id:guid}/publish")]
    public Task<FormVersionResponse> Publish(Guid id, CancellationToken token) => service.PublishAsync(id, token);
    [HttpPost("{id:guid}/drafts")]
    public Task<FormVersionResponse> CreateDraft(Guid id, CancellationToken token) => service.CreateDraftAsync(id, token);
}

public sealed record UpdateFormInput(string Name, bool IsArchived);