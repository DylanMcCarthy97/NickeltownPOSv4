using System.Collections.Generic;
using System.Linq;

namespace NickeltownPOSV4.Models.Migration;

public sealed class MigrationValidationResult
{
    public IReadOnlyList<MigrationValidationIssue> Issues { get; init; } = [];

    public bool HasErrors => Issues.Any(i => i.Severity == MigrationValidationSeverity.Error);

    public bool HasWarnings => Issues.Any(i => i.Severity == MigrationValidationSeverity.Warning);
}
