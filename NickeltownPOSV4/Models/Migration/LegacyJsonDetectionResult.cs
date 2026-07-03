using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Migration;

public sealed class LegacyJsonDetectionResult
{
    public required string RootFolder { get; init; }

    public IReadOnlyList<LegacyDetectedFile> Files { get; init; } = [];

    public bool HasAnyFiles => Files.Count > 0;
}
