using CoachHub.Domain.Assessments;
using CoachHub.Domain.Clients;

namespace CoachHub.Application.Assessments.Importing;

public sealed record ImportColumnMappingInput(
    string ExternalColumnKey, string Header, Guid QuestionStableKey);
public sealed record SaveImportProfileInput(
    Guid FormDefinitionId, string Name, string SheetName,
    string FormCodeHeader, string TimestampHeader, string? ExternalIdHeader,
    IReadOnlyList<ImportColumnMappingInput> Mappings);
public sealed record ImportColumnMappingResponse(
    Guid Id, string ExternalColumnKey, string Header, Guid QuestionStableKey);
public sealed record ImportProfileResponse(
    Guid Id, Guid FormDefinitionId, string Name, string SheetName,
    string FormCodeHeader, string TimestampHeader, string? ExternalIdHeader,
    IReadOnlyList<ImportColumnMappingResponse> Mappings);

public enum ImportRowStatus
{
    Imported,
    SkippedDuplicate,
    Invalid,
    UnmappedQuestion,
    UnknownClientOrCode
}
public sealed record ImportDiagnostic(int RowNumber, ImportRowStatus Status, string Message, string? Column = null);
public sealed record AssessmentImportSummary(
    int Imported, int SkippedDuplicate, int Invalid,
    int UnmappedQuestion, int UnknownClientOrCode,
    IReadOnlyList<ImportDiagnostic> Diagnostics);

public sealed record ParsedWorkbookRow(int RowNumber, IReadOnlyDictionary<string, string?> Cells);
public sealed record ParsedWorksheet(
    string Name, IReadOnlyList<string> Headers, IReadOnlyList<ParsedWorkbookRow> Rows);
public sealed record ImportProfileGraph(
    FormImportProfile Profile, IReadOnlyList<FormImportColumnMapping> Mappings);

public interface IAssessmentWorkbookParser
{
    Task<ParsedWorksheet> ParseAsync(Stream stream, string sheetName, CancellationToken cancellationToken);
}

public interface IFormImportRepository
{
    Task<ImportProfileGraph?> FindProfileAsync(Guid id, CancellationToken cancellationToken);
    Task AddProfileAsync(FormImportProfile profile, IReadOnlyList<FormImportColumnMapping> mappings, CancellationToken cancellationToken);
    Task ReplaceProfileAsync(FormImportProfile profile, IReadOnlyList<FormImportColumnMapping> mappings, CancellationToken cancellationToken);
    Task<Client?> FindClientByFormCodeAsync(string formCode, CancellationToken cancellationToken);
    Task<bool> ImportFingerprintExistsAsync(string fingerprint, CancellationToken cancellationToken);
    Task<bool> TrySubmitAsync(FormSubmission submission, IReadOnlyList<FormAnswer> answers, CancellationToken cancellationToken);
}