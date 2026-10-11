using CoachHub.Application.Auth;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Auth;

public sealed class IdentityGateway(UserManager<User> userManager, CoachHubDbContext db) : IIdentityGateway
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string login,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(login) ?? await userManager.FindByNameAsync(login);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        var coachId = await ResolveCoachAsync(user, cancellationToken);
        if (user.CoachId.HasValue && coachId is null)
        {
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
            user.Email ?? login,
            user.DisplayName,
            roles.ToArray(),
            coachId,
            user.ClientId);
    }

    /// <summary>
    /// The coach whose tenant the login works in: its own coach for Coach accounts (only while
    /// that coach is active), or the client's current coach for Client accounts.
    /// </summary>
    private async Task<Guid?> ResolveCoachAsync(User user, CancellationToken cancellationToken)
    {
        if (user.CoachId is { } coachId)
        {
            return await db.Set<Coach>().AsNoTracking()
                .AnyAsync(coach => coach.Id == coachId && coach.IsActive, cancellationToken)
                ? coachId
                : null;
        }

        if (user.ClientId is { } clientId)
        {
            return await db.Set<Client>().IgnoreQueryFilters().AsNoTracking()
                .Where(client => client.Id == clientId)
                .Select(client => (Guid?)client.CoachId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return null;
    }
}
