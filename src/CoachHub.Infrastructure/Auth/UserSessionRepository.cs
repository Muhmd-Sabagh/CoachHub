using CoachHub.Application.Auth.Sessions;
using CoachHub.Domain.Auth;
using CoachHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Auth;

public sealed class UserSessionRepository(CoachHubDbContext dbContext) : IUserSessionRepository
{
    public async Task AddAsync(UserSession session, CancellationToken cancellationToken)
    {
        dbContext.Set<UserSession>().Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<UserSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.Set<UserSession>()
            .Where(session => session.TokenHash == tokenHash || session.PreviousTokenHash == tokenHash)
            .OrderByDescending(session => session.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<UserSession?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<UserSession>().SingleOrDefaultAsync(session => session.Id == id, cancellationToken);

    public async Task<IReadOnlyList<UserSession>> ListActiveAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await dbContext.Set<UserSession>().AsNoTracking()
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.ExpiresAt > now)
            .OrderByDescending(session => session.LastSeenAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);

    public async Task RevokeAllAsync(
        Guid userId,
        Guid? exceptSessionId,
        SessionEndReason reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await dbContext.Set<UserSession>()
            .Where(session => session.UserId == userId && session.RevokedAt == null && session.ExpiresAt > now)
            .Where(session => exceptSessionId == null || session.Id != exceptSessionId)
            .ToArrayAsync(cancellationToken);
        foreach (var session in active) session.Revoke(now, reason);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
