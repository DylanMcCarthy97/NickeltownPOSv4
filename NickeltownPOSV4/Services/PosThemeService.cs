using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Themes;
using Windows.UI;

namespace NickeltownPOSV4.Services;

public sealed class PosThemeService : IPosThemeService
{
    private static readonly IReadOnlyList<UiThemeId> RecommendedThemeOrder =
    [
        UiThemeId.Light,
        UiThemeId.Cream,
        UiThemeId.Warm,
        UiThemeId.FlounderersRed,
        UiThemeId.PitstopDark,
    ];

    private static readonly IReadOnlyList<UiThemeId> MoreThemeOrder =
    [
        UiThemeId.Dark, UiThemeId.Slate, UiThemeId.Ocean, UiThemeId.Forest,
        UiThemeId.Midnight, UiThemeId.Sunset, UiThemeId.Nord, UiThemeId.Ember, UiThemeId.Rose, UiThemeId.Lavender,
        UiThemeId.Mint, UiThemeId.Coffee, UiThemeId.Arctic, UiThemeId.Grape, UiThemeId.Outback, UiThemeId.IronOre,
        UiThemeId.NeonNight, UiThemeId.TerminalGreen, UiThemeId.DesertSunset, UiThemeId.SteelBlue,
        UiThemeId.Workshop, UiThemeId.TrackDay, UiThemeId.Eucalyptus,
    ];

    private static readonly IReadOnlyList<UiThemeId> ThemeOrder =
    [
        .. RecommendedThemeOrder,
        .. MoreThemeOrder,
    ];

    public UiThemeId CurrentThemeId => AppTheme.CurrentThemeId;

    public event EventHandler? ThemeChanged;

    public IReadOnlyList<UiThemeId> AllThemeIds => ThemeOrder;

    public IReadOnlyList<UiThemeId> RecommendedThemeIds => RecommendedThemeOrder;

    public IReadOnlyList<UiThemeId> MoreThemeIds => MoreThemeOrder;

    public string GetDisplayName(UiThemeId id) => id switch
    {
        UiThemeId.Light => "Light (recommended)",
        UiThemeId.Cream => "Cream (recommended)",
        UiThemeId.Warm => "Warm (recommended)",
        UiThemeId.FlounderersRed => "Flounderers red (recommended)",
        UiThemeId.PitstopDark => "Pitstop dark (recommended)",
        UiThemeId.Dark => "Dark",
        UiThemeId.Slate => "Slate",
        UiThemeId.Ocean => "Ocean",
        UiThemeId.Forest => "Forest",
        UiThemeId.Midnight => "Midnight",
        UiThemeId.Sunset => "Sunset",
        UiThemeId.Nord => "Nord",
        UiThemeId.Ember => "Ember",
        UiThemeId.Rose => "Rose",
        UiThemeId.Lavender => "Lavender",
        UiThemeId.Mint => "Mint",
        UiThemeId.Coffee => "Coffee",
        UiThemeId.Arctic => "Arctic",
        UiThemeId.Grape => "Grape",
        UiThemeId.Outback => "Outback",
        UiThemeId.IronOre => "Iron ore",
        UiThemeId.NeonNight => "Neon night",
        UiThemeId.TerminalGreen => "Terminal green",
        UiThemeId.DesertSunset => "Desert sunset",
        UiThemeId.SteelBlue => "Steel blue",
        UiThemeId.Workshop => "Workshop",
        UiThemeId.TrackDay => "Track day",
        UiThemeId.Eucalyptus => "Eucalyptus",
        _ => id.ToString(),
    };

    public void Apply(UiThemeId themeId)
    {
        AppTheme.Apply(themeId);
        PushBrushesToApplicationResources();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PushBrushesToApplicationResources()
    {
        if (Application.Current?.Resources is not ResourceDictionary resources)
        {
            return;
        }

        SetBrush(resources, "PosCanvasBrush", AppTheme.Background);
        SetBrush(resources, "PosSurfaceBrush", AppTheme.Card);
        SetBrush(resources, "PosSurfaceAltBrush", AppTheme.CardAlt);
        SetBrush(resources, "PosBorderBrush", AppTheme.Border);
        SetBrush(resources, "PosAccentBrush", AppTheme.Accent);
        SetBrush(resources, "PosTextPrimaryBrush", AppTheme.TextPrimary);
        SetBrush(resources, "PosTextSecondaryBrush", AppTheme.TextSecondary);
        SetBrush(resources, "PosNavBarBrush", AppTheme.CardAlt);
        SetBrush(resources, "PosBalanceNegativeBrush", AppTheme.Danger);
        SetBrush(resources, "PosBalanceLowBrush", AppTheme.Warning);
        SetBrush(resources, "PosBalanceGoodBrush", AppTheme.Success);
        SetBrush(resources, "PosBalanceSettledBrush", AppTheme.TextSecondary);
        SetBrush(resources, "SettingsCardBorderBrush", AppTheme.Border);
        SetBrush(resources, "SettingsCardBgBrush", AppTheme.Card);
        SetBrush(resources, "SettingsCardElevatedBgBrush", AppTheme.CardAlt);
        SetBrush(resources, "SettingsTapFieldBgBrush", AppTheme.CardAlt);
        SetBrush(resources, "SettingsTapFieldBorderBrush", AppTheme.Border);
        SetBrush(resources, "SettingsPagerIdleBrush", AppTheme.CardAlt);
        SetBrush(resources, "SettingsPagerHoverBrush", AppTheme.Border);
        SetBrush(resources, "SettingsPagerBorderBrush", AppTheme.Border);
        SetBrush(resources, "PosTabCardSelectedFill", AppTheme.AccentOverlaySoft);

        SetBrush(resources, "PosAccentMutedBrush", AppTheme.AccentOverlaySoft);
        var guestPurple = Color.FromArgb(255, 0xA8, 0x55, 0xF7);
        SetBrush(resources, "PosGuestBrush", guestPurple);
        SetBrush(resources, "PosGuestMutedBrush", Color.FromArgb(0x33, guestPurple.R, guestPurple.G, guestPurple.B));
        SetBrush(resources, "PosSelectedCardBrush", AppTheme.AccentOverlaySoft);
        SetBrush(resources, "PosSelectedCardBorderBrush", AppTheme.Accent);
        SetBrush(resources, "PosButtonPrimaryBrush", AppTheme.Accent);
        SetBrush(resources, "PosSuccessBrush", AppTheme.Success);
        SetBrush(resources, "PosButtonSuccessBrush", AppTheme.Success);
        SetBrush(resources, "PosButtonDangerBrush", AppTheme.Danger);
        SetBrush(resources, "PosButtonNeutralBrush", AppTheme.TextPrimary);
        SetBrush(resources, "PosButtonDarkBrush", AppTheme.TextPrimary);
        SetBrush(resources, "PosButtonSecondaryBrush", AppTheme.TextSecondary);
        SetBrush(resources, "PosModeSegmentSelectedBrush", AppTheme.Accent);
        SetBrush(resources, "PosModeSegmentIdleBrush", AppTheme.Card);
        SetBrush(resources, "PosGuestBadgeFillBrush", Color.FromArgb(0x33, guestPurple.R, guestPurple.G, guestPurple.B));
        SetBrush(resources, "PosGuestBadgeBorderBrush", Color.FromArgb(0x66, guestPurple.R, guestPurple.G, guestPurple.B));
        SetBrush(resources, "PosMemberBadgeFillBrush", Color.FromArgb(0x14, AppTheme.TextPrimary.R, AppTheme.TextPrimary.G, AppTheme.TextPrimary.B));
        SetBrush(resources, "PosMemberBadgeBorderBrush", Color.FromArgb(0x33, AppTheme.TextPrimary.R, AppTheme.TextPrimary.G, AppTheme.TextPrimary.B));
        SetBrush(resources, "PosAddCardBorderBrush", Color.FromArgb(0x66, AppTheme.Accent.R, AppTheme.Accent.G, AppTheme.Accent.B));
        SetBrush(resources, "PosAddGuestCardBorderBrush", Color.FromArgb(0x66, guestPurple.R, guestPurple.G, guestPurple.B));
        SetBrush(resources, "PosScrimBrush", Color.FromArgb(0xB3, AppTheme.TextPrimary.R, AppTheme.TextPrimary.G, AppTheme.TextPrimary.B));
        SetBrush(resources, "PosOnAccentForegroundBrush", PickOnAccentForeground(AppTheme.Accent));

        SetBrush(resources, "PosPanelCanvasBrush", AppTheme.CardAlt);
        SetBrush(resources, "PosPanelMutedSurfaceBrush", AppTheme.Card);
        SetBrush(resources, "PosPanelBorderBrush", AppTheme.Border);
        SetBrush(resources, "PosPanelHeaderBorderBrush", AppTheme.Border);
        SetBrush(resources, "PosChipSelectedFillBrush", AppTheme.AccentOverlaySoft);
        SetBrush(resources, "PosHeaderChipFillBrush", AppTheme.CardAlt);
        SetBrush(resources, "PosHeaderChipBorderBrush", AppTheme.Border);
        SetBrush(resources, "PosInCartHighlightBrush", AppTheme.AccentOverlaySoft);
        SetBrush(resources, "PosProductThumbFillBrush", AppTheme.CardAlt);
        SetBrush(resources, "PosProductThumbBorderBrush", AppTheme.Border);
        SetBrush(resources, "PosCartLineFillBrush", AppTheme.Card);
        SetBrush(resources, "PosCartQtyBadgeBrush", AppTheme.Accent);

        SetBrush(resources, "PosWorkflowShoppingBrush", AppTheme.Accent);
        SetBrush(resources, "PosWorkflowReceiveBrush", AppTheme.Success);
        SetBrush(resources, "PosWorkflowCountBrush", Color.FromArgb(255, 0x7C, 0x3A, 0xED));

        SetBrush(resources, "NumpadDigitBgBrush", AppTheme.Card);
        SetBrush(resources, "NumpadDigitBorderBrush", AppTheme.Border);
        SetBrush(resources, "NumpadActionBgBrush", AppTheme.AccentOverlaySoft);
        SetBrush(resources, "NumpadClearBgBrush", Color.FromArgb(0xFF, 0xFF, 0xF1, 0xF2));
        SetBrush(resources, "NumpadClearBorderBrush", Color.FromArgb(0x55, AppTheme.Danger.R, AppTheme.Danger.G, AppTheme.Danger.B));
        SetBrush(resources, "NumpadClearFgBrush", AppTheme.Danger);
        SetBrush(resources, "NumpadShellBgBrush", AppTheme.CardAlt);
        SetBrush(resources, "NumpadShellBorderBrush", AppTheme.Border);

        SetBrush(resources, "LoginKeyDigitIdleBrush", AppTheme.CardAlt);
        SetBrush(resources, "LoginKeyDigitBorderBrush", Color.FromArgb(0x26, AppTheme.TextPrimary.R, AppTheme.TextPrimary.G, AppTheme.TextPrimary.B));
        SetBrush(resources, "LoginKeyClearIdleBrush", Color.FromArgb(0xFF, 0xFF, 0xF7, 0xED));
        SetBrush(resources, "LoginKeyClearBorderBrush", Color.FromArgb(0x55, AppTheme.Warning.R, AppTheme.Warning.G, AppTheme.Warning.B));

        var darkChrome = ResolveDarkChromeColor();
        SetBrush(resources, "PosHeaderBackgroundBrush", darkChrome);
        SetBrush(resources, "PosFooterBackgroundBrush", darkChrome);
        SetBrush(resources, "PosFooterDividerBrush", ResolveDarkChromeDivider(darkChrome));
        SetBrush(resources, "PosFooterNavActiveBrush", AppTheme.Accent);
        SetBrush(resources, "PosFooterNavIdleBrush", Color.FromArgb(0, darkChrome.R, darkChrome.G, darkChrome.B));
        SetBrush(resources, "PosOnDarkForegroundBrush", Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        SetBrush(resources, "PosOnDarkSecondaryForegroundBrush", Color.FromArgb(0xFF, 0xCB, 0xD5, 0xE1));

        SetBrush(resources, "PosTabStripCreditBrush", AppTheme.Success);
        SetBrush(resources, "PosTabStripOwingBrush", AppTheme.Danger);
        SetBrush(resources, "PosTabStripLowBrush", AppTheme.Warning);
        SetBrush(resources, "PosTabStripSettledBrush", AppTheme.TextSecondary);
        SetBrush(resources, "PosTabStripGuestBrush", guestPurple);

        resources["SettingsHeroBrush"] = BuildHeroGradient(AppTheme.Accent, AppTheme.AccentHover);
        resources["SettingsHeroAccentBrush"] = BuildHeroGradient(
            Lighten(AppTheme.Accent, 0.35),
            Lighten(AppTheme.AccentHover, 0.25));
        resources["LoginEnterAccentBrush"] = BuildHeroGradient(AppTheme.Accent, AppTheme.AccentHover);
        resources["LoginHeroBackgroundBrush"] = BuildLoginBackgroundGradient(darkChrome);
    }

    private static LinearGradientBrush BuildLoginBackgroundGradient(Color darkChrome)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1.15, 1.2),
        };
        brush.GradientStops.Add(new GradientStop { Offset = 0, Color = darkChrome });
        brush.GradientStops.Add(new GradientStop { Offset = 0.38, Color = Blend(darkChrome, AppTheme.Accent, 0.55) });
        brush.GradientStops.Add(new GradientStop { Offset = 0.72, Color = Blend(darkChrome, AppTheme.Accent, 0.35) });
        brush.GradientStops.Add(new GradientStop { Offset = 1, Color = Blend(darkChrome, AppTheme.DrinkOrderIn, 0.45) });
        return brush;
    }

    private static Color PickOnAccentForeground(Color accent)
    {
        var luminance = (0.299 * accent.R) + (0.587 * accent.G) + (0.114 * accent.B);
        return luminance > 160
            ? Color.FromArgb(255, 0x0F, 0x17, 0x2A)
            : Color.FromArgb(255, 255, 255, 255);
    }

    private static Color ResolveDarkChromeColor()
    {
        var t = AppTheme.TextPrimary;
        var lum = (0.299 * t.R) + (0.587 * t.G) + (0.114 * t.B);
        if (lum <= 60)
        {
            return Color.FromArgb(0xFF, t.R, t.G, t.B);
        }

        return Color.FromArgb(0xFF, 0x0F, 0x17, 0x2A);
    }

    private static Color ResolveDarkChromeDivider(Color chrome)
    {
        var r = (byte)Math.Min(255, chrome.R + 0x10);
        var g = (byte)Math.Min(255, chrome.G + 0x10);
        var b = (byte)Math.Min(255, chrome.B + 0x10);
        return Color.FromArgb(0xFF, r, g, b);
    }

    private static LinearGradientBrush BuildHeroGradient(Color start, Color end)
    {
        var hero = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 0.6),
        };
        hero.GradientStops.Add(new GradientStop { Offset = 0, Color = start });
        hero.GradientStops.Add(new GradientStop { Offset = 0.55, Color = end });
        hero.GradientStops.Add(new GradientStop { Offset = 1, Color = Lighten(end, 0.15) });
        return hero;
    }

    private static Color Lighten(Color color, double amount)
    {
        static byte Mix(byte channel, double mix) => (byte)Math.Min(255, channel + ((255 - channel) * mix));
        return Color.FromArgb(color.A, Mix(color.R, amount), Mix(color.G, amount), Mix(color.B, amount));
    }

    private static Color Blend(Color a, Color b, double t)
    {
        static byte Lerp(byte x, byte y, double w) => (byte)(x + ((y - x) * w));
        return Color.FromArgb(
            0xFF,
            Lerp(a.R, b.R, t),
            Lerp(a.G, b.G, t),
            Lerp(a.B, b.B, t));
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        if (resources[key] is SolidColorBrush existing)
        {
            existing.Color = color;
        }
        else
        {
            resources[key] = new SolidColorBrush(color);
        }
    }
}
