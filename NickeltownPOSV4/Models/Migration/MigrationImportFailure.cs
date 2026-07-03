namespace NickeltownPOSV4.Models.Migration;

public sealed class MigrationImportFailure
{
    public required string Message { get; init; }

    public LegacyJsonFileKind? FileKind { get; init; }

    public string? SourceFile { get; init; }

    public string? RecordKey { get; init; }

    public string? JsonPointer { get; init; }
}
