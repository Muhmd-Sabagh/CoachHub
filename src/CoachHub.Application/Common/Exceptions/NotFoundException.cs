namespace CoachHub.Application.Common.Exceptions;

public sealed class NotFoundException(string resourceName, object resourceKey)
    : ApplicationExceptionBase($"{resourceName} '{resourceKey}' was not found.");
