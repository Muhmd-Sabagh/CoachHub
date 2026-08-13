namespace CoachHub.Application.ReferenceData;

public interface IReferenceResponse
{
    Guid Id { get; }
}

public sealed record BilingualReferenceInput(string NameEn, string? NameAr, bool IsActive = true);
public sealed record PackageInput(string NameEn, string? NameAr, string? Description, bool IsActive = true);
public sealed record CurrencyInput(string Code, string Name, string? Symbol, bool IsActive = true);
public sealed record PaymentAccountInput(string Name, string? Details, bool IsActive = true);

public sealed record BilingualReferenceResponse(Guid Id, string NameEn, string? NameAr, bool IsActive) : IReferenceResponse;
public sealed record PackageResponse(Guid Id, string NameEn, string? NameAr, string? Description, bool IsActive) : IReferenceResponse;
public sealed record CurrencyResponse(Guid Id, string Code, string Name, string? Symbol, bool IsActive) : IReferenceResponse;
public sealed record PaymentAccountResponse(Guid Id, string Name, string? Details, bool IsActive) : IReferenceResponse;