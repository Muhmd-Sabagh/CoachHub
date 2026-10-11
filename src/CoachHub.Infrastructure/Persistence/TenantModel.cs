using System.Linq.Expressions;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace CoachHub.Infrastructure.Persistence;

/// <summary>
/// Central tenant isolation (D1, §62). Every tenant-owned entity gets a coach foreign key and a
/// global query filter, so no repository can forget to scope its queries.
/// </summary>
internal static class TenantModel
{
    public static void Apply(ModelBuilder modelBuilder, CoachHubDbContext context)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToArray())
        {
            var clrType = entityType.ClrType;
            var required = typeof(ITenantOwned).IsAssignableFrom(clrType);
            var optional = typeof(IOptionalTenantOwned).IsAssignableFrom(clrType);
            if (!required && !optional) continue;

            var builder = modelBuilder.Entity(clrType);
            builder.HasOne(typeof(Coach)).WithMany()
                .HasForeignKey(nameof(ITenantOwned.CoachId))
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasQueryFilter(BuildFilter(clrType, context));
        }
    }

    /// <summary>
    /// <c>Unrestricted || (CoachId == current coach &amp;&amp; (!client scope || row belongs to the client))</c>.
    /// Anonymous callers have no coach, so they match nothing.
    /// </summary>
    private static LambdaExpression BuildFilter(Type clrType, CoachHubDbContext context)
    {
        var entity = Expression.Parameter(clrType, "entity");
        var tenant = Expression.Constant(context);
        var unrestricted = Expression.Property(tenant, nameof(CoachHubDbContext.TenantUnrestricted));
        var clientScoped = Expression.Property(tenant, nameof(CoachHubDbContext.TenantClientScoped));
        var currentCoach = Expression.Property(tenant, nameof(CoachHubDbContext.TenantCoachId));
        var currentClient = Expression.Property(tenant, nameof(CoachHubDbContext.TenantClientId));

        var coachId = Expression.Property(entity, nameof(ITenantOwned.CoachId));
        var sameCoach = Expression.Equal(coachId, Expression.Convert(currentCoach, coachId.Type));

        Expression ownRecord = Expression.Constant(true);
        if (clrType == typeof(Client))
        {
            ownRecord = Expression.Equal(Expression.Property(entity, nameof(Client.Id)), currentClient);
        }
        else if (clrType.GetProperty("ClientId") is { } clientProperty &&
                 (clientProperty.PropertyType == typeof(Guid) || clientProperty.PropertyType == typeof(Guid?)))
        {
            var clientId = Expression.Property(entity, clientProperty);
            ownRecord = Expression.Equal(clientId, Expression.Convert(currentClient, clientId.Type));
        }

        var body = Expression.OrElse(
            unrestricted,
            Expression.AndAlso(sameCoach, Expression.OrElse(Expression.Not(clientScoped), ownRecord)));
        return Expression.Lambda(body, entity);
    }
}
