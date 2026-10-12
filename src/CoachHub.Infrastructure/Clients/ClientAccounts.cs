using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Clients;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Communications;
using CoachHub.Domain.Auth;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Communications;
using CoachHub.Infrastructure.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoachHub.Infrastructure.Clients;

/// <summary>Invitation links stay valid for a week; a password reset link only for the default day.</summary>
public sealed class InvitationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "Invitation";
    public const string Purpose = "SetInitialPassword";

    public InvitationTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = TimeSpan.FromDays(7);
    }
}

public sealed class InvitationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<InvitationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<User>> logger)
    : DataProtectorTokenProvider<User>(dataProtectionProvider, options, logger);

/// <summary>
/// Client logins (onboarding, §2, §3). The coach invites; the client sets their own password
/// from the emailed link, which goes through the existing password-reset page and endpoint.
/// </summary>
public sealed class ClientAccounts(
    UserManager<User> users,
    RoleManager<Role> roles,
    NotificationService notifications,
    SessionService sessions,
    IOptions<AuthExperienceOptions> experience) : IClientAccounts
{
    public async Task<ClientAccountResponse> GetAsync(Guid clientId, CancellationToken cancellationToken) =>
        Map(await FindAsync(clientId, cancellationToken));

    public async Task<ClientAccountResponse> InviteAsync(Client client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(client.Email))
            throw Validation("email", "An email address is needed to invite the client.");
        var resetUrl = experience.Value.PasswordResetUrl;
        if (string.IsNullOrWhiteSpace(resetUrl)) throw new InvalidOperationException("The password reset URL is not configured.");

        var user = await FindAsync(client.Id, cancellationToken);
        if (user is null)
        {
            if (await users.FindByEmailAsync(client.Email) is not null || await users.FindByNameAsync(client.Email) is not null)
                throw new ConflictException("Another account already uses this email address.");
            user = new User
            {
                UserName = client.Email,
                Email = client.Email,
                EmailConfirmed = true,
                DisplayName = client.Name,
                IsActive = client.IsActive,
                ClientId = client.Id,
                LockoutEnabled = true
            };
            AccountRules.Ensure(await users.CreateAsync(user));
            await AccountRules.EnsureRoleAsync(roles, AuthRoles.Client);
            AccountRules.Ensure(await users.AddToRoleAsync(user, AuthRoles.Client));
        }
        else if (await users.HasPasswordAsync(user))
        {
            throw new ConflictException("The client already set a password; they can use “Forgot password” if needed.");
        }

        var token = await users.GenerateUserTokenAsync(user, InvitationTokenProviderOptions.ProviderName, InvitationTokenProviderOptions.Purpose);
        var link = $"{resetUrl}?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}&invite=1";
        // Account mail in the coach's workspace: sent under the coach's brand, never blocked by
        // the notification switches.
        await notifications.ScheduleAccountMessageAsync(client.CoachId, new(client.Id, NotificationChannel.Email, user.Email!,
            "Your account is ready",
            $"Hello {client.Name},\n\nYour coach invited you. Choose your password with this link (valid for 7 days):\n{link}", null),
            cancellationToken);
        return Map(user);
    }

    public async Task SyncAsync(Client client, CancellationToken cancellationToken)
    {
        var user = await FindAsync(client.Id, cancellationToken);
        if (user is null) return;
        var wasActive = user.IsActive;
        user.DisplayName = client.Name;
        user.IsActive = client.IsActive;
        if (client.Email is { } email && !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            // The sign-in name follows the email when it was the email.
            if (string.Equals(user.UserName, user.Email, StringComparison.OrdinalIgnoreCase)) user.UserName = email;
            user.Email = email;
        }
        AccountRules.Ensure(await users.UpdateAsync(user));
        if (wasActive && !user.IsActive)
            await sessions.RevokeAllAsync(user.Id, null, SessionEndReason.AccountDisabled, cancellationToken);
    }

    private Task<User?> FindAsync(Guid clientId, CancellationToken cancellationToken) =>
        users.Users.SingleOrDefaultAsync(user => user.ClientId == clientId, cancellationToken);

    private static ClientAccountResponse Map(User? user) => user switch
    {
        null => new(ClientAccountState.None, null, null),
        { IsActive: false } => new(ClientAccountState.Disabled, user.Email, user.LastLoginAt),
        { PasswordHash: null } => new(ClientAccountState.Invited, user.Email, user.LastLoginAt),
        _ => new(ClientAccountState.Active, user.Email, user.LastLoginAt)
    };

    private static ValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}
