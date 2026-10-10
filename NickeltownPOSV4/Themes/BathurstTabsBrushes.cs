using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NickeltownPOSV4.Themes;

/// <summary>Cached brushes for the Bar Tabs Bathurst overlay. Not pushed into application resources.</summary>
internal static class BathurstTabsBrushes
{
    private static readonly Dictionary<uint, SolidColorBrush> Cache = new();

    public static SolidColorBrush FromArgb(uint argb)
    {
        if (Cache.TryGetValue(argb, out var existing))
        {
            return existing;
        }

        var brush = new SolidColorBrush(ToColor(argb));
        Cache[argb] = brush;
        return brush;
    }

    public static Brush Token(string key) =>
        FromArgb(key switch
        {
            "Surface" => BathurstTabsTheme.Surface,
            "SurfaceAlt" => BathurstTabsTheme.SurfaceAlt,
            "Card" => BathurstTabsTheme.Card,
            "CardSelected" => BathurstTabsTheme.CardSelected,
            "Border" => BathurstTabsTheme.Border,
            "Accent" => BathurstTabsTheme.Accent,
            "AccentDeep" => BathurstTabsTheme.AccentDeep,
            "ModeBar" => BathurstTabsTheme.ModeBar,
            "ModeRow" => BathurstTabsTheme.ModeBar,
            "ModeBorder" => 0x00FFFFFF,
            "TextPrimary" => BathurstTabsTheme.TextPrimary,
            "TextSecondary" => BathurstTabsTheme.TextSecondary,
            "BalanceGood" => BathurstTabsTheme.BalanceGood,
            "BalanceLow" => BathurstTabsTheme.BalanceLow,
            "BalanceNegative" => BathurstTabsTheme.BalanceNegative,
            "BalanceSettled" => BathurstTabsTheme.BalanceSettled,
            "FreeItemTile" => BathurstTabsTheme.FreeItemTile,
            "FreeItemBorder" => BathurstTabsTheme.Border,
            "OnFreeItem" => BathurstTabsTheme.TextPrimary,
            "CountBadge" => 0x33FFFFFF,
            "GuestCountBadge" => 0x33FFFFFF,
            "AddBorder" => BathurstTabsTheme.Accent,
            "AddGuestBorder" => 0xFFA855F7,
            "SelectedOutline" => BathurstTabsTheme.Accent,
            "Overlay" => BathurstTabsTheme.Overlay,
            "ModeIdle" => 0x33FFFFFF,
            "OnAccent" => 0xFFFFFFFF,
            _ => BathurstTabsTheme.Canvas,
        });

    public static Brush LookupPos(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)
        {
            return b;
        }

        return FromArgb(0xFF64748B);
    }

    public static string PosKey(string token) =>
        token switch
        {
            "Surface" => "PosSurfaceBrush",
            "SurfaceAlt" => "PosSurfaceAltBrush",
            "Card" => "PosSurfaceBrush",
            "CardSelected" => "PosTabCardSelectedFill",
            "Border" => "PosBorderBrush",
            "Accent" => "PosAccentBrush",
            "AccentDeep" => "PosAccentBrush",
            "ModeBar" => "PosModeSegmentIdleBrush",
            "ModeRow" => "PosCanvasBrush",
            "ModeBorder" => "PosBorderBrush",
            "TextPrimary" => "PosTextPrimaryBrush",
            "TextSecondary" => "PosTextSecondaryBrush",
            "BalanceGood" => "PosBalanceGoodBrush",
            "BalanceLow" => "PosBalanceLowBrush",
            "BalanceNegative" => "PosBalanceNegativeBrush",
            "BalanceSettled" => "PosBalanceSettledBrush",
            "FreeItemTile" => "PosButtonPrimaryBrush",
            "FreeItemBorder" => "PosButtonPrimaryBrush",
            "OnFreeItem" => "PosOnAccentForegroundBrush",
            "CountBadge" => "PosAccentMutedBrush",
            "GuestCountBadge" => "PosGuestMutedBrush",
            "AddBorder" => "PosAddCardBorderBrush",
            "AddGuestBorder" => "PosAddGuestCardBorderBrush",
            "SelectedOutline" => "PosSelectedCardBorderBrush",
            "Overlay" => "PosScrimBrush",
            "ModeIdle" => "PosModeSegmentIdleBrush",
            "OnAccent" => "PosOnAccentForegroundBrush",
            _ => "PosCanvasBrush",
        };

    private static Color ToColor(uint argb) =>
        Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
}
