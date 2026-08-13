using CoachHub.Domain.Common;

namespace CoachHub.Domain.ReferenceData;

public interface IBilingualReference
{
    string NameEn { get; }
    string? NameAr { get; }
}

public abstract class ActiveReferenceEntity : Entity
{
    public bool IsActive { get; private set; } = true;

    public void SetActive(bool isActive) => IsActive = isActive;

    protected static string Required(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }

    protected static string? Optional(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }
}