using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NickeltownPOSV4.Themes;

/// <summary>Bathurst Race Day colours for Bar Tabs only. Maps Pos* brush keys used by the tabs board.</summary>
internal static class BathurstTabsPalette
{
    public static readonly Color Canvas = ColorFromHex(0xFF101216);
    public static readonly Color Card = ColorFromHex(0xFF1B1F25);
    public static readonly Color CardAlt = ColorFromHex(0xFF161A20);
    public static readonly Color Border = ColorFromHex(0xFF3A404A);
    public static readonly Color Accent = ColorFromHex(0xFFE32636);
    public static readonly Color AccentMuted = ColorFromHex(0x55E32636);
    public static readonly Color TextPrimary = ColorFromHex(0xFFF5F5F5);
    public static readonly Color TextMuted = ColorFromHex(0xFFB8BEC7);
    public static readonly Color Credit = ColorFromHex(0xFF6DE58B);
    public static readonly Color LowCredit = ColorFromHex(0xFFFFC268);
    public static readonly Color Owing = ColorFromHex(0xFFFF7575);
    public static readonly Color Settled = ColorFromHex(0xFFC4CAD2);
    public static readonly Color SelectedFill = ColorFromHex(0xFF22181A);
    public static readonly Color ButtonPrimary = Accent;
    public static readonly Color ButtonSuccess = ColorFromHex(0xFF2F9E5F);
    public static readonly Color ButtonDark = ColorFromHex(0xFF2A2F38);
    public static readonly Color ButtonSecondary = ColorFromHex(0xFF3A404A);
    public static readonly Color ButtonDanger = ColorFromHex(0xFF8B1E28);
    public static readonly Color FreeItem = ColorFromHex(0xFF2F6FED);
    public static readonly Color ModeSelected = Accent;
    public static readonly Color ModeIdle = ColorFromHex(0xFF1B1F25);
    public static readonly Color OnAccent = Colors.White;
    public static readonly Color Guest = ColorFromHex(0xFF7C6CFF);
    public static readonly Color GuestMuted = ColorFromHex(0x557C6CFF);
    public static readonly Color Scrim = ColorFromHex(0xCC0A0C10);
    public static readonly Color AddCardBorder = Accent;

    private static readonly Dictionary<string, Color> PosKeyColors = new()
    {
        ["PosCanvasBrush"] = Canvas,
        ["PosSurfaceBrush"] = Card,
        ["PosSurfaceAltBrush"] = CardAlt,
        ["PosBorderBrush"] = Border,
        ["PosAccentBrush"] = Accent,
        ["PosAccentMutedBrush"] = AccentMuted,
        ["PosTextPrimaryBrush"] = TextPrimary,
        ["PosTextSecondaryBrush"] = TextMuted,
        ["PosBalanceNegativeBrush"] = Owing,
        ["PosBalanceLowBrush"] = LowCredit,
        ["PosBalanceGoodBrush"] = Credit,
        ["PosBalanceSettledBrush"] = Settled,
        ["PosTabCardSelectedFill"] = SelectedFill,
        ["PosSelectedCardBrush"] = SelectedFill,
        ["PosSelectedCardBorderBrush"] = Accent,
        ["PosButtonPrimaryBrush"] = ButtonPrimary,
        ["PosButtonSuccessBrush"] = ButtonSuccess,
        ["PosSuccessBrush"] = ButtonSuccess,
        ["PosButtonDangerBrush"] = ButtonDanger,
        ["PosButtonDarkBrush"] = ButtonDark,
        ["PosButtonNeutralBrush"] = ButtonDark,
        ["PosButtonSecondaryBrush"] = ButtonSecondary,
        ["PosOnAccentForegroundBrush"] = OnAccent,
        ["PosOnDarkForegroundBrush"] = OnAccent,
        ["PosOnDarkSecondaryForegroundBrush"] = TextMuted,
        ["PosGuestBrush"] = Guest,
        ["PosGuestMutedBrush"] = GuestMuted,
        ["PosScrimBrush"] = Scrim,
        ["PosAddCardBorderBrush"] = AddCardBorder,
        ["PosAddGuestCardBorderBrush"] = Guest,
        ["PosModeSegmentSelectedBrush"] = ModeSelected,
        ["PosModeSegmentIdleBrush"] = ModeIdle,
        ["PosHeaderBackgroundBrush"] = Canvas,
        ["PosTabStripCreditBrush"] = Credit,
        ["PosTabStripOwingBrush"] = Owing,
        ["PosTabStripLowBrush"] = LowCredit,
        ["PosTabStripSettledBrush"] = Settled,
        ["PosTabStripGuestBrush"] = Guest,
    };

    private static readonly Dictionary<string, SolidColorBrush> BrushCache = new();

    public static bool TryGetColor(string posKey, out Color color) =>
        PosKeyColors.TryGetValue(posKey, out color);

    public static bool TryGetBrush(string posKey, out Brush brush)
    {
        if (!PosKeyColors.TryGetValue(posKey, out var color))
        {
            brush = null!;
            return false;
        }

        if (!BrushCache.TryGetValue(posKey, out var cached))
        {
            cached = new SolidColorBrush(color);
            BrushCache[posKey] = cached;
        }

        brush = cached;
        return true;
    }

    public static IReadOnlyDictionary<string, Color> AllPosKeyColors => PosKeyColors;

    private static Color ColorFromHex(uint argb) =>
        Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
}
