namespace NickeltownPOSV4.Models.Migration;

public enum MigrationValidationSeverity
{
    Information = 0,
    Warning,
    Error,
}

public sealed class MigrationValidationIssue
{
    public required MigrationValidationSeverity Severity { get; init; }

    public required string Message { get; init; }

    public string? SourceFile { get; init; }

    public LegacyJsonFileKind? FileKind { get; init; }

    public string? JsonPath { get; init; }
}
