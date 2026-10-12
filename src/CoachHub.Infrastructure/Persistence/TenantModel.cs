using System.Linq.Expressions;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Domain.Common;
using CoachHub.Domain.Media;
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

        if (clrType == typeof(MediaAsset))
        {
            return MediaFilter(entity, tenant, unrestricted, clientScoped, sameCoach, ownRecord);
        }
        if (typeof(ICatalogEntry).IsAssignableFrom(clrType))
        {
            return CatalogFilter(entity, tenant, unrestricted, sameCoach);
        }

        var body = Expression.OrElse(
            unrestricted,
            Expression.AndAlso(sameCoach, Expression.OrElse(Expression.Not(clientScoped), ownRecord)));
        return Expression.Lambda(body, entity);
    }

    /// <summary>
    /// Catalog visibility (see <see cref="ICatalogEntry"/>): <c>Unrestricted || (signed in &amp;&amp; platform entry)
    /// || CoachId == current coach</c>. A client's current coach is their coach, so clients see their
    /// coach's custom entries too.
    /// </summary>
    private static LambdaExpression CatalogFilter(
        ParameterExpression entity, Expression tenant, Expression unrestricted, Expression sameCoach)
    {
        var signedIn = Expression.Property(tenant, nameof(CoachHubDbContext.TenantSignedIn));
        var noCoach = Expression.Equal(
            Expression.Property(entity, nameof(ICatalogEntry.CoachId)), Expression.Constant(null, typeof(Guid?)));
        var body = Expression.OrElse(unrestricted, Expression.OrElse(Expression.AndAlso(signedIn, noCoach), sameCoach));
        return Expression.Lambda(body, entity);
    }

    /// <summary>
    /// Media visibility (see <see cref="MediaAsset"/>): <c>Unrestricted || (signed in &amp;&amp; platform media)
    /// || (CoachId == current coach &amp;&amp; (!client scope || coach library || the client's own media))</c>.
    /// </summary>
    private static LambdaExpression MediaFilter(
        ParameterExpression entity,
        Expression tenant,
        Expression unrestricted,
        Expression clientScoped,
        Expression sameCoach,
        Expression ownRecord)
    {
        var signedIn = Expression.Property(tenant, nameof(CoachHubDbContext.TenantSignedIn));
        var noCoach = Expression.Equal(
            Expression.Property(entity, nameof(MediaAsset.CoachId)), Expression.Constant(null, typeof(Guid?)));
        var noClient = Expression.Equal(
            Expression.Property(entity, nameof(MediaAsset.ClientId)), Expression.Constant(null, typeof(Guid?)));

        var platform = Expression.AndAlso(signedIn, noCoach);
        var tenantMedia = Expression.AndAlso(
            sameCoach,
            Expression.OrElse(Expression.Not(clientScoped), Expression.OrElse(noClient, ownRecord)));
        var body = Expression.OrElse(unrestricted, Expression.OrElse(platform, tenantMedia));
        return Expression.Lambda(body, entity);
    }
}
