using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Migration;

public sealed class MigrationImportResult
{
    public required MigrationRunContext Run { get; init; }

    public IReadOnlyList<MigrationSegmentResult> Segments { get; init; } = [];

    public IReadOnlyList<MigrationImportFailure> GlobalFailures { get; init; } = [];

    public string? BackupFolder { get; init; }

    public string? LogFilePath { get; init; }
}
