using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Migration;

public sealed class MigrationSegmentResult
{
    public required LegacyJsonFileKind Kind { get; init; }

    public int Attempted { get; init; }

    public int Imported { get; init; }

    public int SkippedDuplicate { get; init; }

    public IReadOnlyList<MigrationImportFailure> Failures { get; init; } = [];
}
