using CoachHub.Application.Auth;
using CoachHub.Application.Auth.Sessions;
using CoachHub.Application.Coaches;
using CoachHub.Application.Common.Exceptions;
using CoachHub.Application.Common.Models;
using CoachHub.Domain.Auth;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Infrastructure.Auth;
using CoachHub.Infrastructure.Auth.Persistence;
using CoachHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Coaches;

/// <summary>Administrator management of coaches (tenants) and their login accounts (§2 Admin, §56).</summary>
public sealed class CoachAdministrationService(
    CoachHubDbContext db,
    UserManager<User> users,
    RoleManager<Role> roles,
    SessionService sessions,
    TimeProvider timeProvider) : ICoachAdministrationService
{
    public async Task<PagedResult<CoachResponse>> ListAsync(CoachQuery query, CancellationToken cancellationToken)
    {
        query = query.Normalize();
        var coaches = db.Set<Coach>().AsNoTracking();
        if (query.SearchTerm is { } term) coaches = coaches.Where(coach => coach.DisplayName.Contains(term));
        if (query.IsActive.HasValue) coaches = coaches.Where(coach => coach.IsActive == query.IsActive.Value);
        var total = await coaches.LongCountAsync(cancellationToken);
        var page = await coaches.OrderBy(coach => coach.DisplayName).ThenBy(coach => coach.Id)
            .Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var items = new List<CoachResponse>(page.Length);
        foreach (var coach in page) items.Add(await MapAsync(coach, cancellationToken));
        return new(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<CoachResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await MapAsync(await FindRequiredAsync(id, cancellationToken), cancellationToken);

    public async Task<CoachResponse> CreateAsync(CoachCreateInput input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(input.DisplayName)) errors["displayName"] = ["A display name is required."];
        else if (input.DisplayName.Trim().Length > 200) errors["displayName"] = ["Display name cannot exceed 200 characters."];
        if (string.IsNullOrWhiteSpace(input.Email)) errors["email"] = ["An email address is required."];
        if (errors.Count > 0) throw new ValidationException(errors);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var coach = Coach.Create(input.DisplayName, timeProvider.GetUtcNow());
        db.Add(coach);
        await db.SaveChangesAsync(cancellationToken);

        var user = new User
        {
            UserName = AccountRules.UserName(input.UserName, input.Email),
            Email = input.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = coach.DisplayName,
            IsActive = true,
            CoachId = coach.Id,
            LockoutEnabled = true
        };
        var created = string.IsNullOrWhiteSpace(input.Password)
            ? await users.CreateAsync(user)
            : await users.CreateAsync(user, input.Password);
        AccountRules.Ensure(created);
        await AccountRules.EnsureRoleAsync(roles, AuthRoles.Coach);
        AccountRules.Ensure(await users.AddToRoleAsync(user, AuthRoles.Coach));
        await transaction.CommitAsync(cancellationToken);
        return await MapAsync(coach, cancellationToken);
    }

    public async Task<CoachResponse> UpdateAsync(Guid id, CoachUpdateInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Trim().Length > 200)
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["displayName"] = ["A display name of at most 200 characters is required."]
            });

        var coach = await FindRequiredAsync(id, cancellationToken);
        coach.Update(input.DisplayName, input.IsActive);
        var user = await users.Users.SingleOrDefaultAsync(account => account.CoachId == coach.Id, cancellationToken);
        if (user is not null)
        {
            user.DisplayName = coach.DisplayName;
            user.IsActive = coach.IsActive;
            AccountRules.Ensure(await users.UpdateAsync(user));
        }
        await db.SaveChangesAsync(cancellationToken);
        if (!coach.IsActive && user is not null)
            await sessions.RevokeAllAsync(user.Id, null, SessionEndReason.AccountDisabled, cancellationToken);
        return await MapAsync(coach, cancellationToken);
    }

    private async Task<Coach> FindRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Set<Coach>().SingleOrDefaultAsync(coach => coach.Id == id, cancellationToken)
        ?? throw new NotFoundException("Coach", id);

    private async Task<CoachResponse> MapAsync(Coach coach, CancellationToken cancellationToken)
    {
        var user = await users.Users.AsNoTracking()
            .SingleOrDefaultAsync(account => account.CoachId == coach.Id, cancellationToken);
        var clientCount = await db.Set<Client>().IgnoreQueryFilters()
            .CountAsync(client => client.CoachId == coach.Id, cancellationToken);
        return new(coach.Id, coach.DisplayName, coach.IsActive, coach.CreatedAt,
            user?.Id, user?.Email, user?.UserName, clientCount);
    }
}
