using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Communications;
using CoachHub.Domain.Auth;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Communications;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoachHub.Infrastructure.Auth;

public sealed class AuthExperienceOptions
{
    public const string SectionName = "Authentication:Experience";
    public string PasswordResetUrl { get; init; } = string.Empty;
}

/// <summary>
/// Administrator and Client login accounts. Coach accounts are managed with their coach by
/// <see cref="Coaches.CoachAdministrationService"/>.
/// </summary>
public sealed class AccountService(UserManager<User> users, RoleManager<Role> roles, CoachHubDbContext db,
    NotificationService notifications, SessionService sessions, IOptions<AuthExperienceOptions> options) : IAccountService
{
    public async Task<IReadOnlyList<AccountResponse>> ListAsync(CancellationToken token)
    {
        var rows = await users.Users.AsNoTracking().OrderBy(x => x.Email).Take(500).ToArrayAsync(token);
        var output = new List<AccountResponse>(rows.Length);
        foreach (var user in rows) output.Add(await Map(user));
        return output;
    }

    public async Task<AccountResponse> CreateAsync(CreateAccountInput input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var role = await ValidateAsync(input, token);
        var user = new User
        {
            UserName = AccountRules.UserName(input.UserName, input.Email),
            Email = input.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = input.DisplayName.Trim(),
            IsActive = true,
            ClientId = role == AuthRoles.Client ? input.ClientId : null,
            LockoutEnabled = true
        };
        AccountRules.Ensure(string.IsNullOrWhiteSpace(input.Password)
            ? await users.CreateAsync(user)
            : await users.CreateAsync(user, input.Password));
        await AccountRules.EnsureRoleAsync(roles, role);
        AccountRules.Ensure(await users.AddToRoleAsync(user, role));
        return await Map(user);
    }

    public async Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountInput input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Trim().Length > 200)
            throw Validation("displayName", "A display name of at most 200 characters is required.");
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("Account", id);
        if (user.CoachId.HasValue)
            throw new ConflictException("Coach accounts are managed through coach administration.");
        user.DisplayName = input.DisplayName.Trim();
        user.IsActive = input.IsActive;
        AccountRules.Ensure(await users.UpdateAsync(user));
        if (!user.IsActive) await sessions.RevokeAllAsync(user.Id, null, SessionEndReason.AccountDisabled, token);
        return await Map(user);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken token)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email)) return;
        var resetUrl = options.Value.PasswordResetUrl;
        if (string.IsNullOrWhiteSpace(resetUrl)) throw new InvalidOperationException("Password reset URL is not configured.");
        var code = await users.GeneratePasswordResetTokenAsync(user);
        var url = $"{resetUrl}?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(code)}";
        await notifications.SchedulePlatformAsync(new(user.ClientId, NotificationChannel.Email, user.Email,
            "Password reset", $"Use this one-time link to reset your password: {url}", null), token);
    }

    public async Task ResetPasswordAsync(PasswordResetInput input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null || !user.IsActive) throw Validation("token", "The reset request is invalid or expired.");
        if (await IsInvitationAsync(user, input.Token))
        {
            // A client choosing their first password from the coach's invitation (onboarding).
            AccountRules.Ensure(await users.AddPasswordAsync(user, input.NewPassword));
            AccountRules.Ensure(await users.UpdateSecurityStampAsync(user)); // The invitation link is used up.
            return;
        }
        AccountRules.Ensure(await users.ResetPasswordAsync(user, input.Token, input.NewPassword));
        // Whoever knew the old password may hold a session; a reset signs every device out.
        await sessions.RevokeAllAsync(user.Id, null, SessionEndReason.PasswordReset, token);
    }

    /// <summary>An invitation only sets a first password; once one exists, the link no longer works.</summary>
    private async Task<bool> IsInvitationAsync(User user, string token) =>
        !await users.HasPasswordAsync(user) &&
        await users.VerifyUserTokenAsync(user, Clients.InvitationTokenProviderOptions.ProviderName,
            Clients.InvitationTokenProviderOptions.Purpose, token);

    private async Task<string> ValidateAsync(CreateAccountInput input, CancellationToken token)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.Email)) errors["email"] = ["An email address is required."];
        if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Trim().Length > 200)
            errors["displayName"] = ["A display name of at most 200 characters is required."];
        var role = AuthRoles.All.SingleOrDefault(x => string.Equals(x, input.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (role is null || role == AuthRoles.Coach)
            errors["role"] = ["Choose Administrator or Client. Coach accounts are created with their coach."];
        else if (role == AuthRoles.Client && !input.ClientId.HasValue)
            errors["clientId"] = ["A Client account must be linked to a client."];
        else if (role != AuthRoles.Client && input.ClientId.HasValue)
            errors["clientId"] = ["Only Client accounts can be linked to a client."];
        if (errors.Count > 0) throw new ValidationException(errors);

        if (role == AuthRoles.Client)
        {
            if (!await db.Set<Client>().AnyAsync(x => x.Id == input.ClientId, token))
                throw new NotFoundException("Client", input.ClientId!.Value);
            if (await users.Users.AnyAsync(x => x.ClientId == input.ClientId, token))
                throw new ConflictException("The client already has a login account.");
        }
        return role!;
    }

    private async Task<AccountResponse> Map(User user) => new(user.Id, user.Email ?? string.Empty,
        user.UserName ?? string.Empty, user.DisplayName, user.IsActive,
        (await users.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty, user.CoachId, user.ClientId);

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
