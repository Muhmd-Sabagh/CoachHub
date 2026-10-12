namespace CoachHub.Application.Common.Exceptions;

/// <summary>
/// Runs domain code and turns its <see cref="ArgumentException"/> invariants into a field-keyed
/// <see cref="ValidationException"/> (400) named after the offending parameter.
/// </summary>
public static class DomainValidation
{
    public static void Run(Action action, string fallbackField)
    {
        try
        {
            action();
        }
        catch (ArgumentException exception)
        {
            throw ToValidation(exception, fallbackField);
        }
    }

    public static T Run<T>(Func<T> action, string fallbackField)
    {
        try
        {
            return action();
        }
        catch (ArgumentException exception)
        {
            throw ToValidation(exception, fallbackField);
        }
    }

    private static ValidationException ToValidation(ArgumentException exception, string fallbackField)
    {
        var field = string.IsNullOrWhiteSpace(exception.ParamName) ? fallbackField : exception.ParamName;
        field = char.ToLowerInvariant(field[0]) + field[1..];
        var message = exception.ParamName is null
            ? exception.Message
            : exception.Message.Replace($" (Parameter '{exception.ParamName}')", string.Empty, StringComparison.Ordinal);
        if (exception is ArgumentOutOfRangeException && message.StartsWith("Specified argument", StringComparison.Ordinal))
            message = "The value is out of range.";
        return new ValidationException(new Dictionary<string, string[]> { [field] = [message] });
    }
}
