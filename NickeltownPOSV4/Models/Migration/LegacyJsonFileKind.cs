namespace NickeltownPOSV4.Models.Migration;

/// <summary>Known legacy V2 JSON artifacts. Additional kinds can be added as schema mapping matures.</summary>
public enum LegacyJsonFileKind
{
    Unknown = 0,
    Drinks,
    Items,
    Tabs,
    Members,
    Bartenders,
    PitstopSalesData,
    SquareConfig,
    SettingsOrConfig,

    /// <summary>Category / department list (import before items/drinks when present).</summary>
    Categories,
}
