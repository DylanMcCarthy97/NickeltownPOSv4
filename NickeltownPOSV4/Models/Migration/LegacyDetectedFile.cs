namespace NickeltownPOSV4.Models.Migration;

public sealed class LegacyDetectedFile
{
    public required LegacyJsonFileKind Kind { get; init; }

    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public long LengthBytes { get; init; }
}
