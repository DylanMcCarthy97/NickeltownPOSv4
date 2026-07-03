namespace NickeltownPOSV4.Models;

public sealed class TabEditorRow
{
    public required string LegacyId { get; init; }

    public required string DisplayName { get; init; }

    public bool IsMember { get; init; }

    public bool IsGuest { get; init; }

    public string? Notes { get; init; }

    public string? MemberId { get; init; }
}
