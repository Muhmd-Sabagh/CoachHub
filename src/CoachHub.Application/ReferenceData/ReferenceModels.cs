namespace CoachHub.Application.ReferenceData;

public interface IReferenceResponse
{
    Guid Id { get; }
}

public sealed record BilingualReferenceInput(string NameEn, string? NameAr, bool IsActive = true);
/// <param name="CoachId">Required when an administrator creates the package; ignored for coaches.</param>
public sealed record PackageInput(string NameEn, string? NameAr, string? Description, bool IsActive = true, Guid? CoachId = null);
public sealed record CurrencyInput(string Code, string Name, string? Symbol, bool IsActive = true);
/// <param name="CoachId">Required when an administrator creates the account; ignored for coaches.</param>
public sealed record PaymentAccountInput(string Name, string? Details, bool IsActive = true, Guid? CoachId = null);

public sealed record BilingualReferenceResponse(Guid Id, string NameEn, string? NameAr, bool IsActive) : IReferenceResponse;
public sealed record PackageResponse(Guid Id, Guid CoachId, string NameEn, string? NameAr, string? Description, bool IsActive) : IReferenceResponse;
public sealed record CurrencyResponse(Guid Id, string Code, string Name, string? Symbol, bool IsActive) : IReferenceResponse;
public sealed record PaymentAccountResponse(Guid Id, Guid CoachId, string Name, string? Details, bool IsActive) : IReferenceResponse;