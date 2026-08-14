namespace CoachHub.Application.Auth;

public sealed record AccountResponse(Guid Id, string Email, string DisplayName, bool IsActive, Guid? ClientId, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public sealed record CreateAccountInput(string Email, string DisplayName, string? Password, Guid? ClientId, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public sealed record UpdateAccountInput(string DisplayName, bool IsActive, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);
public sealed record PasswordResetRequest(string Email);
public sealed record PasswordResetInput(string Email, string Token, string NewPassword);

public interface IAccountService
{
    Task<IReadOnlyList<AccountResponse>> ListAsync(CancellationToken token);
    Task<AccountResponse> CreateAsync(CreateAccountInput input, CancellationToken token);
    Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountInput input, CancellationToken token);
    Task RequestPasswordResetAsync(string email, CancellationToken token);
    Task ResetPasswordAsync(PasswordResetInput input, CancellationToken token);
}
