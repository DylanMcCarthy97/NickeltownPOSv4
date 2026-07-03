namespace NickeltownPOSV4.Models;

/// <summary>Optional member link when creating a member tab.</summary>
public sealed class MemberPickerRow
{
    public required string LegacyId { get; init; }

    public required string DisplayName { get; init; }
}
