using CoachHub.Domain.Common;

namespace CoachHub.Domain.Assessments;

public sealed class FormImportProfile : Entity
{
    private FormImportProfile() { }
    public Guid FormDefinitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string SheetName { get; private set; } = string.Empty;
    public string FormCodeHeader { get; private set; } = string.Empty;
    public string TimestampHeader { get; private set; } = string.Empty;
    public string? ExternalIdHeader { get; private set; }

    public static FormImportProfile Create(
        Guid definitionId, string name, string sheetName,
        string formCodeHeader, string timestampHeader, string? externalIdHeader)
    {
        if (definitionId == Guid.Empty) throw new ArgumentException("Form is required.", nameof(definitionId));
        return new FormImportProfile { FormDefinitionId = definitionId }.Update(
            name, sheetName, formCodeHeader, timestampHeader, externalIdHeader);
    }

    public FormImportProfile Update(
        string name, string sheetName, string formCodeHeader,
        string timestampHeader, string? externalIdHeader)
    {
        Name = Required(name, 200, nameof(name));
        SheetName = Required(sheetName, 200, nameof(sheetName));
        FormCodeHeader = Required(formCodeHeader, 500, nameof(formCodeHeader));
        TimestampHeader = Required(timestampHeader, 500, nameof(timestampHeader));
        ExternalIdHeader = Optional(externalIdHeader, 500, nameof(externalIdHeader));
        return this;
    }

    private static string Required(string value, int maximum, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameter);
        var normalized = value.Trim();
        if (normalized.Length > maximum) throw new ArgumentOutOfRangeException(parameter);
        return normalized;
    }
    private static string? Optional(string? value, int maximum, string parameter) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, maximum, parameter);
}