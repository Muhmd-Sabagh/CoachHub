using CoachHub.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoachHub.API.Auth;

[ApiController]
[Route("api/accounts")]
[Authorize(Policy = AuthPermissions.ManageUsers)]
public sealed class AccountsController(IAccountService service) : ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<AccountResponse>> List(CancellationToken token) => service.ListAsync(token);
    [HttpPost] public async Task<ActionResult<AccountResponse>> Create(CreateAccountInput input, CancellationToken token) { var created = await service.CreateAsync(input, token); return Created($"/api/accounts/{created.Id}", created); }
    [HttpPut("{id:guid}")] public Task<AccountResponse> Update(Guid id, UpdateAccountInput input, CancellationToken token) => service.UpdateAsync(id, input, token);
}
