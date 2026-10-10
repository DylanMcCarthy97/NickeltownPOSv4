namespace NickeltownPOSV4.Models.Settings;

/// <summary>
/// Installation-level Bar Tabs visual preference (shared SQLite Settings table).
/// Not per-user; survives logout/restart. Default is Normal (Bathurst off).
/// </summary>
public sealed class TabsVisualThemePreferences
{
    public const string SettingsKey = "tabs_visual_theme.v1";

    /// <summary>When true, Bar Tabs uses the Bathurst Race Day overlay.</summary>
    public bool IsBathurstTheme { get; set; }
}
