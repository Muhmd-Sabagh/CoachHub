namespace CoachHub.Domain.Common;

/// <summary>
/// Keeps a property out of the audit trail (D2): secrets such as token hashes and access codes,
/// and bookkeeping fields whose changes alone are not an auditable operation.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute;
