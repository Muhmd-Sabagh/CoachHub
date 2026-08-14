using System.Security.Claims;
using CoachHub.Application.Auth;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Communications;
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

public sealed class AccountService(UserManager<User> users, RoleManager<Role> roles, CoachHubDbContext db,
    NotificationService notifications, IOptions<AuthExperienceOptions> options) : IAccountService
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
        token.ThrowIfCancellationRequested(); Validate(input.Roles, input.Permissions, input.ClientId);
        if (input.ClientId.HasValue && !await db.Set<Client>().AnyAsync(x => x.Id == input.ClientId, token)) throw new NotFoundException("Client", input.ClientId.Value);
        var user = new User { UserName = input.Email.Trim(), Email = input.Email.Trim(), EmailConfirmed = true,
            DisplayName = input.DisplayName.Trim(), IsActive = true, ClientId = input.ClientId, LockoutEnabled = true };
        var result = string.IsNullOrWhiteSpace(input.Password) ? await users.CreateAsync(user) : await users.CreateAsync(user, input.Password);
        Ensure(result, "create account"); await ApplyAccess(user, input.Roles, input.Permissions);
        return await Map(user);
    }

    public async Task<AccountResponse> UpdateAsync(Guid id, UpdateAccountInput input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Validate(input.Roles, input.Permissions, null, validateClientRole: false);
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new NotFoundException("Account", id);
        if (input.Roles.Contains(AuthRoles.Client, StringComparer.OrdinalIgnoreCase) != user.ClientId.HasValue)
            throw new ArgumentException("Client role accounts must remain linked to exactly one client.");
        user.DisplayName = input.DisplayName.Trim(); user.IsActive = input.IsActive; Ensure(await users.UpdateAsync(user), "update account");
        await ApplyAccess(user, input.Roles, input.Permissions); return await Map(user);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken token)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email)) return;
        var resetUrl = options.Value.PasswordResetUrl;
        if (string.IsNullOrWhiteSpace(resetUrl)) throw new InvalidOperationException("Password reset URL is not configured.");
        var code = await users.GeneratePasswordResetTokenAsync(user);
        var url = $"{resetUrl}?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(code)}";
        await notifications.ScheduleAsync(new(user.ClientId, NotificationChannel.Email, user.Email,
            "CoachHub password reset", $"Use this one-time link to reset your password: {url}", null), token);
    }

    public async Task ResetPasswordAsync(PasswordResetInput input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null || !user.IsActive) throw new ValidationException(new Dictionary<string, string[]> { ["token"] = ["The reset request is invalid or expired."] });
        Ensure(await users.ResetPasswordAsync(user, input.Token, input.NewPassword), "reset password");
    }

    private async Task ApplyAccess(User user, IReadOnlyList<string> requestedRoles, IReadOnlyList<string> permissions)
    {
        var desiredRoles = requestedRoles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var role in desiredRoles) if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new Role { Name = role }), "create role");
        var current = await users.GetRolesAsync(user); var remove = current.Except(desiredRoles, StringComparer.OrdinalIgnoreCase).ToArray(); var add = desiredRoles.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();
        if (remove.Length > 0) Ensure(await users.RemoveFromRolesAsync(user, remove), "remove roles"); if (add.Length > 0) Ensure(await users.AddToRolesAsync(user, add), "add roles");
        var claims = await users.GetClaimsAsync(user); var old = claims.Where(x => x.Type == AuthPermissions.ClaimType).ToArray(); if (old.Length > 0) Ensure(await users.RemoveClaimsAsync(user, old), "remove permissions");
        var desired = permissions.Distinct(StringComparer.Ordinal).Select(x => new Claim(AuthPermissions.ClaimType, x)).ToArray(); if (desired.Length > 0) Ensure(await users.AddClaimsAsync(user, desired), "add permissions");
    }

    private async Task<AccountResponse> Map(User user) => new(user.Id, user.Email ?? string.Empty, user.DisplayName, user.IsActive, user.ClientId,
        (await users.GetRolesAsync(user)).ToArray(), (await users.GetClaimsAsync(user)).Where(x => x.Type == AuthPermissions.ClaimType).Select(x => x.Value).Order().ToArray());
    private static void Validate(IReadOnlyList<string> requestedRoles, IReadOnlyList<string> permissions, Guid? clientId, bool validateClientRole = true)
    {
        var allowedRoles = new[] { AuthRoles.Administrator, AuthRoles.Staff, AuthRoles.Client };
        if (requestedRoles.Count == 0 || requestedRoles.Any(x => !allowedRoles.Contains(x, StringComparer.OrdinalIgnoreCase))) throw new ArgumentException("Only Administrator, Staff, and Client roles are supported.");
        if (permissions.Any(x => !AuthPermissions.All.Contains(x, StringComparer.Ordinal))) throw new ArgumentException("An unknown permission was requested.");
        if (validateClientRole && requestedRoles.Contains(AuthRoles.Client, StringComparer.OrdinalIgnoreCase) != clientId.HasValue) throw new ArgumentException("Client role accounts must be linked to exactly one client.");
    }
    private static void Ensure(IdentityResult result, string operation) { if (!result.Succeeded) throw new InvalidOperationException($"Unable to {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}"); }
}
