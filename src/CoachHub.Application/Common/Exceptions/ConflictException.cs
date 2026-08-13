namespace CoachHub.Application.Common.Exceptions;

public sealed class ConflictException(string message) : ApplicationExceptionBase(message);
