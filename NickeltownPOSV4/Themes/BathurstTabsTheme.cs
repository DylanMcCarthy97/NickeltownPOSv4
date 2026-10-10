using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Themes;

/// <summary>
/// Bathurst 1000 race-day tokens for the Bar Tabs workspace only.
/// Does not replace the global <see cref="AppTheme"/> palette.
/// </summary>
public static class BathurstTabsTheme
{
    public const string SettingsKey = "ui.bathurst_tabs_theme.v1";

    public const string EventTitle = "BATHURST 1000";

    public const string CircuitName = "MOUNT PANORAMA";

    public const string Tagline = "GREATEST RACE ON EARTH";

    public const string RaceDayEdition = "RACE DAY EDITION | 11 OCTOBER 2026";

    public const string Branding = "Nickeltown POS";

    /// <summary>True when Appearances is set to Bathurst 1000. Default is off.</summary>
    public static bool IsEnabled { get; internal set; }

    public static uint Canvas { get; } = 0xFF0B0B0D;
    public static uint Surface { get; } = 0xFF141416;
    public static uint SurfaceAlt { get; } = 0xFF1A1A1E;
    public static uint Card { get; } = 0xFF161618;
    public static uint CardSelected { get; } = 0xFF221014;
    public static uint Border { get; } = 0xFF3A3A40;
    public static uint Accent { get; } = 0xFFE10600;
    public static uint AccentDeep { get; } = 0xFF9B0D24;
    public static uint ModeBar { get; } = 0xFFC8102E;
    public static uint TextPrimary { get; } = 0xFFF8FAFC;
    public static uint TextSecondary { get; } = 0xFFA1A1AA;
    public static uint BalanceGood { get; } = 0xFF22C55E;
    public static uint BalanceLow { get; } = 0xFFF59E0B;
    public static uint BalanceNegative { get; } = 0xFFEF4444;
    public static uint BalanceSettled { get; } = 0xFF94A3B8;
    public static uint FreeItemTile { get; } = 0xFF1F1F24;
    public static uint Overlay { get; } = 0xCC0B0B0D;

    public static uint BalanceColor(TabBalanceTier tier) =>
        tier switch
        {
            TabBalanceTier.Negative => BalanceNegative,
            TabBalanceTier.Low => BalanceLow,
            TabBalanceTier.Settled => BalanceSettled,
            _ => BalanceGood,
        };

    public static uint StripColor(bool isGuest, TabBalanceTier tier) =>
        isGuest ? 0xFFA855F7 : BalanceColor(tier);
}
