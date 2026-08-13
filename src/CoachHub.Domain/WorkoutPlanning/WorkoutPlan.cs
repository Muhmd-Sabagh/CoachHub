using CoachHub.Domain.Common;

namespace CoachHub.Domain.WorkoutPlanning;

public sealed class WorkoutPlan : Entity
{
    private WorkoutPlan() { }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public Guid? ClientId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static WorkoutPlan Create(string nameEn, string? nameAr, Guid? clientId, DateTimeOffset createdAt)
    {
        var plan = new WorkoutPlan { CreatedAt = createdAt };
        plan.Update(nameEn, nameAr, clientId);
        return plan;
    }

    public void Update(string nameEn, string? nameAr, Guid? clientId)
    {
        NameEn = WorkoutText.Required(nameEn, 255, nameof(nameEn));
        NameAr = WorkoutText.Optional(nameAr, 255, nameof(nameAr));
        if (clientId == Guid.Empty) throw new ArgumentException("Client identifier cannot be empty.", nameof(clientId));
        ClientId = clientId;
    }

    public void Assign(Guid? clientId)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("Client identifier cannot be empty.", nameof(clientId));
        ClientId = clientId;
    }
}

internal static class WorkoutText
{
    public static string Required(string value, int maximum, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        if (normalized.Length > maximum) throw new ArgumentOutOfRangeException(parameter);
        return normalized;
    }
    public static string? Optional(string? value, int maximum, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maximum, parameter);
    public static Guid Id(Guid value, string parameter) =>
        value == Guid.Empty ? throw new ArgumentException("Identifier is required.", parameter) : value;
    public static int Order(int value, string parameter) =>
        value < 0 ? throw new ArgumentOutOfRangeException(parameter) : value;
}
