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
            "ModeSelected" => BathurstTabsTheme.ModeSelected,
            "ModeRow" => BathurstTabsTheme.Canvas,
            "ModeBorder" => BathurstTabsTheme.Border,
            "ModeIdle" => BathurstTabsTheme.ModeBar,
            "TextPrimary" => BathurstTabsTheme.TextPrimary,
            "TextSecondary" => BathurstTabsTheme.TextSecondary,
            "BalanceGood" => BathurstTabsTheme.BalanceGood,
            "BalanceLow" => BathurstTabsTheme.BalanceLow,
            "BalanceNegative" => BathurstTabsTheme.BalanceNegative,
            "BalanceSettled" => BathurstTabsTheme.BalanceSettled,
            "FreeItemTile" => BathurstTabsTheme.FreeItemTile,
            "FreeItemBorder" => BathurstTabsTheme.Border,
            "OnFreeItem" => BathurstTabsTheme.TextPrimary,
            "CountBadge" => 0x4DFFFFFF,
            "GuestCountBadge" => 0x4DFFFFFF,
            "AddBorder" => BathurstTabsTheme.Accent,
            "AddGuestBorder" => 0xFFA855F7,
            "SelectedOutline" => BathurstTabsTheme.Accent,
            "Overlay" => BathurstTabsTheme.Overlay,
            "PanelScrim" => BathurstTabsTheme.PanelScrim,
            "ActionPrimary" => BathurstTabsTheme.ActionPrimary,
            "ActionSuccess" => BathurstTabsTheme.ActionSuccess,
            "ActionDark" => BathurstTabsTheme.ActionDark,
            "ActionDanger" => BathurstTabsTheme.ActionDanger,
            "PagerSurface" => BathurstTabsTheme.PagerSurface,
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
            "PanelScrim" => "PosScrimBrush",
            "ActionPrimary" => "PosButtonPrimaryBrush",
            "ActionSuccess" => "PosButtonSuccessBrush",
            "ActionDark" => "PosButtonDarkBrush",
            "ActionDanger" => "PosButtonDangerBrush",
            "PagerSurface" => "PosSurfaceBrush",
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
