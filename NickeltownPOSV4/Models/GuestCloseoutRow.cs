namespace NickeltownPOSV4.Models;

/// <summary>Open guest tab row for end-of-night closeout.</summary>
public sealed class GuestCloseoutRow
{
    public required string LegacyId { get; init; }

    public required string DisplayName { get; init; }

    public decimal Balance { get; init; }

    public string LastActivityText { get; init; } = "—";

    public string CreatedText { get; init; } = "—";
}
