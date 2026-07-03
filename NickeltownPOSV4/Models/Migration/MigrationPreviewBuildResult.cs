using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Migration;

public sealed record MigrationPreviewBuildResult
{
    public required MigrationPreviewCounts Counts { get; init; }

    public required IReadOnlyList<MigrationFilePreviewDiagnostic> Diagnostics { get; init; }
}
