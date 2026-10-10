namespace NickeltownPOSV4.Models.Settings;

/// <summary>Persisted Bar Tabs visual preference only (SQLite Settings). Does not change POS data.</summary>
public sealed class TabsVisualThemePreferences
{
    public const string SettingsKey = "tabs_visual_theme.v1";

    /// <summary>When true, Bar Tabs uses the Bathurst Race Day visual theme. Default false (Normal).</summary>
    public bool IsBathurstTheme { get; set; }
}
