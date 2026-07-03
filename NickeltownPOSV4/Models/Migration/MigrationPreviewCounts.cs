namespace NickeltownPOSV4.Models.Migration;

/// <summary>Approximate entity counts derived from legacy JSON (best-effort until full mapping exists).</summary>
public sealed record MigrationPreviewCounts
{
    public int Drinks { get; init; }

    public int Items { get; init; }

    public int Categories { get; init; }

    public int Tabs { get; init; }

    public int TabHistoryEntries { get; init; }

    public int Members { get; init; }

    public int Bartenders { get; init; }

    public int PitstopSales { get; init; }

    public int SettingsDocuments { get; init; }

    public int SquareConfigRoots { get; init; }

    public int UnreadableOrMalformedFiles { get; init; }
}
