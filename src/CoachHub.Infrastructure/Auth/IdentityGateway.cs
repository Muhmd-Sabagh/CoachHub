using CoachHub.Application.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.AspNetCore.Identity;

namespace CoachHub.Infrastructure.Auth;

public sealed class IdentityGateway(UserManager<User> userManager) : IIdentityGateway
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException("The authenticated user's login timestamp could not be updated.");
        }

        var roles = await userManager.GetRolesAsync(user);

        return new AuthenticatedUser(
            user.Id,
            user.Email ?? email,
            user.DisplayName,
            roles.ToArray());
    }
}
