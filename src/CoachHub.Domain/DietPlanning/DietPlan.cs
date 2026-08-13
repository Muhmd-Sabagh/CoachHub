using CoachHub.Domain.Common;

namespace CoachHub.Domain.DietPlanning;

public sealed class DietPlan : Entity
{
    private DietPlan() { }
    public string NameEn { get; private set; } = string.Empty;
    public string? NameAr { get; private set; }
    public Guid? ClientId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static DietPlan Create(
        string nameEn, string? nameAr, Guid? clientId, DateTimeOffset createdAt)
    {
        var plan = new DietPlan { CreatedAt = createdAt };
        plan.Update(nameEn, nameAr, clientId);
        return plan;
    }
    public void Update(string nameEn, string? nameAr, Guid? clientId)
    {
        NameEn = DietPlanText.Required(nameEn, 255, nameof(nameEn));
        NameAr = DietPlanText.Optional(nameAr, 255, nameof(nameAr));
        if (clientId == Guid.Empty) throw new ArgumentException("Client identifier cannot be empty.", nameof(clientId));
        ClientId = clientId;
    }
    public void Assign(Guid? clientId)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("Client identifier cannot be empty.", nameof(clientId));
        ClientId = clientId;
    }
}

internal static class DietPlanText
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
    public static int Order(int value, string parameter)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(parameter);
        return value;
    }
    public static decimal Quantity(decimal value, string parameter)
    {
        if (value <= 0 || value > 10000m) throw new ArgumentOutOfRangeException(parameter);
        return value;
    }
    public static Guid RequiredId(Guid value, string parameter)
    {
        if (value == Guid.Empty) throw new ArgumentException("Identifier is required.", parameter);
        return value;
    }
}