namespace CoachHub.Application.Auth;

/// <summary>A login account. Every account has exactly one role: Administrator, Coach, or Client.</summary>
public sealed record AccountResponse(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    bool IsActive,
    string Role,
    Guid? CoachId,
    Guid? ClientId);

/// <summary>
/// Creates an Administrator or Client account. Coach accounts are created together with their
/// coach through coach administration.
/// </summary>
/// <param name="UserName">Optional login name; defaults to the email address.</param>
/// <param name="ClientId">Required for, and only allowed on, Client accounts.</param>
public sealed record CreateAccountInput(
    string Email,
    string? UserName,
    string DisplayName,
    string? Password,
    string Role,
    Guid? ClientId);

public sealed record UpdateAccountInput(string DisplayName, bool IsActive);
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
