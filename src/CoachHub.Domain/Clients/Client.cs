using System.Net.Mail;
using CoachHub.Domain.Common;

namespace CoachHub.Domain.Clients;

public sealed class Client : Entity
{
    private readonly List<Subscription> _subscriptions = [];

    private Client() { }

    public string ClientCode { get; private set; } = string.Empty;
    public string FormCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public DateOnly JoinDate { get; private set; }
    public PlanWorkflowStatus DietStatus { get; private set; }
    public PlanWorkflowStatus WorkoutStatus { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<Subscription> Subscriptions => _subscriptions;

    public static Client Create(
        string clientCode,
        string formCode,
        string name,
        string? phone,
        string? email,
        DateOnly joinDate)
    {
        var client = new Client
        {
            ClientCode = Code(clientCode, 8, 50, nameof(clientCode)),
            FormCode = Code(formCode, 10, 50, nameof(formCode)),
            JoinDate = joinDate,
            DietStatus = PlanWorkflowStatus.NotStarted,
            WorkoutStatus = PlanWorkflowStatus.NotStarted,
            IsActive = true
        };
        client.Update(name, phone, email, client.DietStatus, client.WorkoutStatus, true);
        return client;
    }

    public void Update(
        string name,
        string? phone,
        string? email,
        PlanWorkflowStatus dietStatus,
        PlanWorkflowStatus workoutStatus,
        bool isActive)
    {
        Name = Required(name, 255, nameof(name));
        Phone = Optional(phone, 50, nameof(phone));
        Email = NormalizeEmail(email);
        if (!Enum.IsDefined(dietStatus)) throw new ArgumentOutOfRangeException(nameof(dietStatus));
        if (!Enum.IsDefined(workoutStatus)) throw new ArgumentOutOfRangeException(nameof(workoutStatus));
        DietStatus = dietStatus;
        WorkoutStatus = workoutStatus;
        IsActive = isActive;
    }

    public void RegenerateFormCode(string formCode) =>
        FormCode = Code(formCode, 10, 50, nameof(formCode));

    public void AddSubscription(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (subscription.ClientId != Id)
        {
            throw new ArgumentException("Subscription belongs to another client.", nameof(subscription));
        }
        _subscriptions.Add(subscription);
    }
    public SubscriptionStatus GetSubscriptionStatus(DateOnly today)
    {
        if (_subscriptions.Any(subscription => subscription.IsActiveOn(today)))
        {
            return SubscriptionStatus.Active;
        }
        return _subscriptions.Count > 0
            ? SubscriptionStatus.Expired
            : SubscriptionStatus.Inactive;
    }

    private static string Code(string value, int minimumLength, int maximumLength, string parameterName)
    {
        var code = Required(value, maximumLength, parameterName).ToUpperInvariant();
        if (code.Length < minimumLength || code.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException("Codes must use uppercase ASCII letters and digits.", parameterName);
        }
        return code;
    }

    private static string? NormalizeEmail(string? value)
    {
        var normalized = Optional(value, 255, nameof(value));
        if (normalized is null) return null;
        if (!MailAddress.TryCreate(normalized, out var address) ||
            !address.Address.Equals(normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A valid email address is required.", nameof(value));
        }
        return normalized.ToLowerInvariant();
    }

    private static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}