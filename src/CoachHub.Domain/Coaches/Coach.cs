using CoachHub.Domain.Common;

namespace CoachHub.Domain.Coaches;

/// <summary>A coach is the platform tenant: an independent business that owns its clients and data.</summary>
public sealed class Coach : Entity
{
    private Coach() { }

    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Coach Create(string displayName, DateTimeOffset createdAt)
    {
        var coach = new Coach { CreatedAt = createdAt, IsActive = true };
        coach.Update(displayName, true);
        return coach;
    }

    public void Update(string displayName, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName, nameof(displayName));
        var normalized = displayName.Trim();
        if (normalized.Length > 200) throw new ArgumentOutOfRangeException(nameof(displayName));
        DisplayName = normalized;
        IsActive = isActive;
    }
}
