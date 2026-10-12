using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoachHub.Application.Auditing;
using CoachHub.Domain.Auditing;
using CoachHub.Domain.Clients;
using CoachHub.Domain.Coaches;
using CoachHub.Domain.Common;
using CoachHub.Infrastructure.Auth.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CoachHub.Infrastructure.Auditing;

/// <summary>
/// Turns tracked changes into audit entries (D2, §58): who, which role, what, the affected coach
/// and client, and the previous and new values. Secrets are never captured, and an update that
/// only touches bookkeeping fields is not an auditable operation.
/// </summary>
internal static class AuditCapture
{
    public const int MaximumValueLength = 1000;

    // Identity columns that are secrets or derived noise. Our own entities use [AuditIgnore].
    private static readonly HashSet<string> IdentityIgnored =
    [
        nameof(IdentityUser<Guid>.PasswordHash),
        nameof(IdentityUser<Guid>.SecurityStamp),
        nameof(IdentityUser<Guid>.ConcurrencyStamp),
        nameof(IdentityUser<Guid>.NormalizedEmail),
        nameof(IdentityUser<Guid>.NormalizedUserName),
        nameof(IdentityRole<Guid>.NormalizedName)
    ];

    // Whole tables never audited: authenticator and recovery tokens are secrets.
    private static readonly HashSet<Type> IgnoredTypes = [typeof(AuditEntry), typeof(IdentityUserToken<Guid>)];

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<AuditEntry> Capture(
        ChangeTracker changeTracker,
        AuditActor actor,
        Guid? actorCoachId,
        Guid? actorClientId,
        DateTimeOffset occurredAt)
    {
        var tracked = changeTracker.Entries().ToArray();
        var changed = tracked
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(entry => !IgnoredTypes.Contains(entry.Metadata.ClrType))
            .ToArray();
        if (changed.Length == 0) return [];

        // Aggregate children (meals, days, answers) carry no coach or client of their own. When
        // everything tracked in this unit of work belongs to one coach or one client, that is
        // the owner; otherwise fall back to the caller's own tenant.
        var unitCoach = Single(tracked.Select(CoachOf)) ?? actorCoachId;
        var unitClient = Single(tracked.Select(ClientOf)) ?? actorClientId;

        var entries = new List<AuditEntry>(changed.Length);
        foreach (var entry in changed)
        {
            var changes = Changes(entry);
            if (entry.State == EntityState.Modified && changes.Count == 0) continue;

            entries.Add(AuditEntry.Create(
                entry.Metadata.ClrType.Name,
                EntityId(entry),
                Operation(entry.State),
                actor.Kind,
                actor.UserId,
                actor.DisplayName,
                occurredAt,
                CoachOf(entry) ?? unitCoach,
                ClientOf(entry) ?? unitClient,
                changes.Count == 0 ? null : JsonSerializer.Serialize(changes, Json)));
        }
        return entries;
    }

    /// <summary>A hand-written entry for operations that bypass change tracking (bulk updates).</summary>
    public static AuditEntry Record(
        AuditActor actor,
        string entityType,
        Guid? entityId,
        AuditOperation operation,
        Guid? coachId,
        Guid? clientId,
        IReadOnlyDictionary<string, (object? From, object? To)> changes,
        DateTimeOffset occurredAt) =>
        AuditEntry.Create(
            entityType, entityId, operation, actor.Kind, actor.UserId, actor.DisplayName, occurredAt, coachId, clientId,
            JsonSerializer.Serialize(
                changes.ToDictionary(change => change.Key, change => new AuditValue(Value(change.Value.From), Value(change.Value.To))),
                Json));

    private static Dictionary<string, AuditValue> Changes(EntityEntry entry)
    {
        var changes = new Dictionary<string, AuditValue>(StringComparer.Ordinal);
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey() || IsIgnored(entry.Metadata, property.Metadata)) continue;
            switch (entry.State)
            {
                case EntityState.Added when property.CurrentValue is not null:
                    changes[property.Metadata.Name] = new AuditValue(null, Value(property.CurrentValue));
                    break;
                case EntityState.Deleted when property.OriginalValue is not null:
                    changes[property.Metadata.Name] = new AuditValue(Value(property.OriginalValue), null);
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[property.Metadata.Name] = new AuditValue(Value(property.OriginalValue), Value(property.CurrentValue));
                    break;
            }
        }
        return changes;
    }

    private static bool IsIgnored(IEntityType entityType, IProperty property)
    {
        if (property.PropertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>(inherit: true) is not null) return true;
        var clrType = entityType.ClrType;
        return (clrType == typeof(User) || clrType == typeof(Role)) && IdentityIgnored.Contains(property.Name);
    }

    /// <summary>Values as JSON; long text is cut so one entry cannot balloon.</summary>
    private static JsonElement? Value(object? value)
    {
        if (value is null) return null;
        if (value is string text && text.Length > MaximumValueLength) value = text[..MaximumValueLength] + "…";
        try
        {
            return JsonSerializer.SerializeToElement(value, value.GetType(), Json);
        }
        catch (NotSupportedException)
        {
            return JsonSerializer.SerializeToElement(value.ToString(), Json);
        }
    }

    private static Guid? CoachOf(EntityEntry entry) => entry.Entity switch
    {
        Coach coach => coach.Id,
        AuditEntry => null,
        _ => GuidProperty(entry, "CoachId")
    };

    private static Guid? ClientOf(EntityEntry entry) => entry.Entity switch
    {
        Client client => client.Id,
        AuditEntry => null,
        _ => GuidProperty(entry, "ClientId")
    };

    private static Guid? GuidProperty(EntityEntry entry, string name)
    {
        if (entry.Metadata.FindProperty(name) is null) return null;
        var property = entry.Property(name);
        var value = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
        return value is Guid id && id != Guid.Empty ? id : null;
    }

    private static Guid? Single(IEnumerable<Guid?> values)
    {
        var distinct = values.Where(value => value.HasValue).Distinct().Take(2).ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static Guid? EntityId(EntityEntry entry)
    {
        if (entry.Entity is Entity entity) return entity.Id;
        var key = entry.Metadata.FindPrimaryKey();
        return key?.Properties.Count == 1 && entry.Property(key.Properties[0].Name).CurrentValue is Guid id ? id : null;
    }

    private static AuditOperation Operation(EntityState state) => state switch
    {
        EntityState.Added => AuditOperation.Create,
        EntityState.Modified => AuditOperation.Update,
        EntityState.Deleted => AuditOperation.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    private sealed record AuditValue(
        [property: JsonPropertyName("from"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? From,
        [property: JsonPropertyName("to"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? To);
}
