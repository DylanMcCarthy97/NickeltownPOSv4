using NickeltownPOSV4.Models;
using NickeltownPOSV4.Models.Settings;

namespace NickeltownPOSV4.Themes;

/// <summary>
/// Bathurst 1000 race-day tokens for the Bar Tabs workspace only.
/// Driven by <see cref="TabsVisualThemePreferences"/> — not global Appearance.
/// </summary>
public static class BathurstTabsTheme
{
    public const string SettingsKey = TabsVisualThemePreferences.SettingsKey;

    /// <summary>Previous settings key; read once for migration.</summary>
    public const string LegacySettingsKey = "ui.bathurst_tabs_theme.v1";

    public const string EventTitle = "BATHURST 1000";

    public const string Branding = "NICKELTOWN POS";

    public const string RaceDayEdition = "RACE DAY EDITION";

    /// <summary>True when the installation-level Tabs Bathurst preference is on.</summary>
    public static bool IsEnabled { get; internal set; }

    public static uint Canvas { get; } = 0xFF101216;
    public static uint Surface { get; } = 0xFF1B1F25;
    public static uint SurfaceAlt { get; } = 0xFF161A20;
    public static uint Card { get; } = 0xFF1B1F25;
    public static uint CardSelected { get; } = 0xFF22181A;
    public static uint Border { get; } = 0xFF3A404A;
    public static uint Accent { get; } = 0xFFE32636;
    public static uint AccentDeep { get; } = 0xFF8B1E28;
    public static uint ModeBar { get; } = 0xFF1B1F25;
    public static uint ModeSelected { get; } = 0xFFE32636;
    public static uint TextPrimary { get; } = 0xFFF5F5F5;
    public static uint TextSecondary { get; } = 0xFFB8BEC7;
    public static uint BalanceGood { get; } = 0xFF6DE58B;
    public static uint BalanceLow { get; } = 0xFFFFC268;
    public static uint BalanceNegative { get; } = 0xFFFF7575;
    public static uint BalanceSettled { get; } = 0xFFC4CAD2;
    public static uint FreeItemTile { get; } = 0xFF2F6FED;
    public static uint Overlay { get; } = 0xE6101216;

    public static uint BalanceColor(TabBalanceTier tier) =>
        tier switch
        {
            TabBalanceTier.Negative => BalanceNegative,
            TabBalanceTier.Low => BalanceLow,
            TabBalanceTier.Settled => BalanceSettled,
            _ => BalanceGood,
        };

    public static uint StripColor(bool isGuest, TabBalanceTier tier) =>
        isGuest ? 0xFF7C6CFF : BalanceColor(tier);
}
