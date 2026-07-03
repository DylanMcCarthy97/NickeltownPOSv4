using System;
using Windows.UI;

namespace NickeltownPOSV4.Themes
{
    public enum UiThemeId
    {
        Light,
        Dark,
        /// <summary>Dark blue-gray chrome; still uses the same AppTheme tokens as other themes.</summary>
        Slate,
        /// <summary>Warm paper / low-glare light theme.</summary>
        Warm,
        /// <summary>Deep teal / cyan dark theme.</summary>
        Ocean,
        /// <summary>Forest green dark theme.</summary>
        Forest,
        /// <summary>Indigo / violet dark theme.</summary>
        Midnight,
        /// <summary>Warm dusk - deep wine surfaces with sunset orange accent.</summary>
        Sunset,
        /// <summary>Frosted light theme inspired by Nord.</summary>
        Nord,
        /// <summary>Charcoal with ember orange accent.</summary>
        Ember,
        /// <summary>Soft blush light theme with rose accent.</summary>
        Rose,
        /// <summary>Soft purple light theme.</summary>
        Lavender,
        /// <summary>Fresh mint green light theme.</summary>
        Mint,
        /// <summary>Warm espresso dark theme.</summary>
        Coffee,
        /// <summary>Icy blue-gray light theme.</summary>
        Arctic,
        /// <summary>Deep plum / magenta dark theme.</summary>
        Grape,
        /// <summary>Dusty red, sand, earthy Australian outback.</summary>
        Outback,
        /// <summary>Rust and charcoal industrial dark.</summary>
        IronOre,
        /// <summary>Near-black with electric accent.</summary>
        NeonNight,
        /// <summary>Retro terminal black and phosphor green.</summary>
        TerminalGreen,
        /// <summary>Warm orange, coral, dusk.</summary>
        DesertSunset,
        /// <summary>Cool industrial blue-grey.</summary>
        SteelBlue,
        /// <summary>Club red, black, and white.</summary>
        FlounderersRed,
        /// <summary>Dark pit-lane with punchy accent.</summary>
        PitstopDark,
        /// <summary>Grey workshop with hazard yellow.</summary>
        Workshop,
        /// <summary>Black, white, and racing red.</summary>
        TrackDay,
        /// <summary>Soft warm light, low glare.</summary>
        Cream,
        /// <summary>Muted eucalyptus green-grey calm.</summary>
        Eucalyptus
    }

    /// <summary>
    /// Theme palette and helpers. Colors are resolved from the active palette so per-user themes work after login.
    /// </summary>
    public static class AppTheme
    {
        private sealed class ThemePalette
        {
            public Color Background { get; init; }
            public Color Card { get; init; }
            public Color CardAlt { get; init; }
            public Color Border { get; init; }
            public Color Accent { get; init; }
            public Color AccentHover { get; init; }
            public Color TextPrimary { get; init; }
            public Color TextSecondary { get; init; }
            public Color Success { get; init; }
            public Color Danger { get; init; }
            public Color Warning { get; init; }
            public Color RowAlt { get; init; }

            /// <summary>Primary grid / action tile face (Bar + Pitstop).</summary>
            public Color TilePrimary { get; init; }
            public Color TilePrimaryHover { get; init; }
            public Color TileDangerBack { get; init; }
            public Color TileDangerBackHover { get; init; }
            public Color TileDangerForeground { get; init; }
            public Color DrinkRegular { get; init; }
            public Color DrinkOrderIn { get; init; }
            public Color DrinkSpecial { get; init; }
            public Color DrinkSpecialPriceText { get; init; }
            public Color FocusRing { get; init; }
            public Color PressedRing { get; init; }
            public Color ControlStrongBorder { get; init; }
        }

        private static readonly ThemePalette LightPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xF5, 0xF5, 0xF5),
            Card = Color.FromArgb(255, 255, 255, 255),
            CardAlt = Color.FromArgb(255, 0xE5, 0xE5, 0xE5),
            Border = Color.FromArgb(255, 0xCC, 0xCC, 0xCC),
            Accent = Color.FromArgb(255, 0x1E, 0x6F, 0xD9),
            AccentHover = Color.FromArgb(255, 0x3B, 0x82, 0xF6),
            TextPrimary = Color.FromArgb(255, 0x21, 0x21, 0x21),
            TextSecondary = Color.FromArgb(255, 0x75, 0x75, 0x75),
            Success = Color.FromArgb(255, 0x22, 0xC5, 0x5E),
            Danger = Color.FromArgb(255, 0xEF, 0x44, 0x44),
            Warning = Color.FromArgb(255, 0xF5, 0x9E, 0x0B),
            RowAlt = Color.FromArgb(255, 0xF9, 0xF9, 0xF9),
            TilePrimary = Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
            TilePrimaryHover = Color.FromArgb(255, 0xE5, 0xE7, 0xEB),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0xF1, 0xF5, 0xF9),
            DrinkOrderIn = Color.FromArgb(255, 0xA8, 0x55, 0xF7),
            DrinkSpecial = Color.FromArgb(255, 0xFF, 0xE8, 0x8E),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0x3B, 0x82, 0xF6),
            PressedRing = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            ControlStrongBorder = Color.FromArgb(255, 0x0F, 0x17, 0x2A)
        };

        private static readonly ThemePalette DarkPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x1E, 0x1E, 0x1E),
            Card = Color.FromArgb(255, 0x2D, 0x2D, 0x30),
            CardAlt = Color.FromArgb(255, 0x3C, 0x3C, 0x42),
            Border = Color.FromArgb(255, 0x55, 0x55, 0x5C),
            Accent = Color.FromArgb(255, 0x3B, 0x82, 0xF6),
            AccentHover = Color.FromArgb(255, 0x60, 0xA5, 0xFA),
            TextPrimary = Color.FromArgb(255, 0xF3, 0xF4, 0xF6),
            TextSecondary = Color.FromArgb(255, 0x9C, 0xA3, 0xAF),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x33, 0x33, 0x38),
            TilePrimary = Color.FromArgb(255, 0x3F, 0x3F, 0x46),
            TilePrimaryHover = Color.FromArgb(255, 0x52, 0x52, 0x5B),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x2D, 0x2D, 0x30),
            DrinkOrderIn = Color.FromArgb(255, 0x8B, 0x5C, 0xF6),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0x38, 0xBD, 0xF8),
            PressedRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            ControlStrongBorder = Color.FromArgb(255, 0x9C, 0xA3, 0xAF)
        };

        private static readonly ThemePalette SlatePalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x1E, 0x29, 0x3B),
            Card = Color.FromArgb(255, 0x33, 0x41, 0x55),
            CardAlt = Color.FromArgb(255, 0x47, 0x55, 0x69),
            Border = Color.FromArgb(255, 0x64, 0x74, 0x8B),
            Accent = Color.FromArgb(255, 0x38, 0xBD, 0xF8),
            AccentHover = Color.FromArgb(255, 0x7D, 0xD3, 0xFC),
            TextPrimary = Color.FromArgb(255, 0xF1, 0xF5, 0xF9),
            TextSecondary = Color.FromArgb(255, 0x94, 0xA3, 0xB8),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x3D, 0x4D, 0x63),
            TilePrimary = Color.FromArgb(255, 0x47, 0x55, 0x69),
            TilePrimaryHover = Color.FromArgb(255, 0x64, 0x74, 0x8B),
            TileDangerBack = Color.FromArgb(255, 0x4C, 0x05, 0x19),
            TileDangerBackHover = Color.FromArgb(255, 0x88, 0x13, 0x37),
            TileDangerForeground = Color.FromArgb(255, 0xFD, 0xA4, 0xAF),
            DrinkRegular = Color.FromArgb(255, 0x33, 0x41, 0x55),
            DrinkOrderIn = Color.FromArgb(255, 0x6D, 0x28, 0xD9),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0x7D, 0xD3, 0xFC),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0x94, 0xA3, 0xB8)
        };

        private static readonly ThemePalette WarmPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xFA, 0xF7, 0xF2),
            Card = Color.FromArgb(255, 255, 255, 255),
            CardAlt = Color.FromArgb(255, 0xEF, 0xE8, 0xDE),
            Border = Color.FromArgb(255, 0xD4, 0xC4, 0xB0),
            Accent = Color.FromArgb(255, 0xB4, 0x53, 0x09),
            AccentHover = Color.FromArgb(255, 0xD9, 0x77, 0x06),
            TextPrimary = Color.FromArgb(255, 0x29, 0x25, 0x24),
            TextSecondary = Color.FromArgb(255, 0x78, 0x71, 0x6C),
            Success = Color.FromArgb(255, 0x16, 0x83, 0x39),
            Danger = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            Warning = Color.FromArgb(255, 0xCA, 0x8A, 0x04),
            RowAlt = Color.FromArgb(255, 0xF5, 0xEF, 0xE6),
            TilePrimary = Color.FromArgb(255, 0xFF, 0xFB, 0xF7),
            TilePrimaryHover = Color.FromArgb(255, 0xEF, 0xE8, 0xDE),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0x99, 0x1B, 0x1B),
            TileDangerForeground = Color.FromArgb(255, 0xFE, 0xCA, 0xCA),
            DrinkRegular = Color.FromArgb(255, 0xFF, 0xFA, 0xF3),
            DrinkOrderIn = Color.FromArgb(255, 0x7E, 0x22, 0xCE),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0xD9, 0x77, 0x06),
            PressedRing = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            ControlStrongBorder = Color.FromArgb(255, 0x44, 0x40, 0x3C)
        };

        private static readonly ThemePalette OceanPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x0C, 0x18, 0x1C),
            Card = Color.FromArgb(255, 0x14, 0x28, 0x30),
            CardAlt = Color.FromArgb(255, 0x1E, 0x3A, 0x44),
            Border = Color.FromArgb(255, 0x3D, 0x5C, 0x6A),
            Accent = Color.FromArgb(255, 0x2D, 0xD4, 0xBF),
            AccentHover = Color.FromArgb(255, 0x5E, 0xE8, 0xD5),
            TextPrimary = Color.FromArgb(255, 0xEC, 0xFE, 0xFF),
            TextSecondary = Color.FromArgb(255, 0x8A, 0xB4, 0xBE),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x18, 0x32, 0x3A),
            TilePrimary = Color.FromArgb(255, 0x1E, 0x3A, 0x44),
            TilePrimaryHover = Color.FromArgb(255, 0x2A, 0x4A, 0x56),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x14, 0x28, 0x30),
            DrinkOrderIn = Color.FromArgb(255, 0x22, 0xC5, 0x5E),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0x5E, 0xE8, 0xD5),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0x5E, 0x8F, 0x9A)
        };

        private static readonly ThemePalette ForestPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x12, 0x1A, 0x14),
            Card = Color.FromArgb(255, 0x1C, 0x2A, 0x1F),
            CardAlt = Color.FromArgb(255, 0x28, 0x3D, 0x2C),
            Border = Color.FromArgb(255, 0x3D, 0x5A, 0x44),
            Accent = Color.FromArgb(255, 0x4A, 0xD9, 0x5E),
            AccentHover = Color.FromArgb(255, 0x6E, 0xF7, 0x8A),
            TextPrimary = Color.FromArgb(255, 0xF0, 0xFD, 0xF4),
            TextSecondary = Color.FromArgb(255, 0x9C, 0xB8, 0xA4),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x22, 0x35, 0x28),
            TilePrimary = Color.FromArgb(255, 0x28, 0x3D, 0x2C),
            TilePrimaryHover = Color.FromArgb(255, 0x36, 0x52, 0x3C),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x1C, 0x2A, 0x1F),
            DrinkOrderIn = Color.FromArgb(255, 0xA3, 0xE6, 0x35),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0x6E, 0xF7, 0x8A),
            PressedRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            ControlStrongBorder = Color.FromArgb(255, 0x6B, 0x8F, 0x78)
        };

        private static readonly ThemePalette MidnightPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x0E, 0x0B, 0x16),
            Card = Color.FromArgb(255, 0x1A, 0x16, 0x2E),
            CardAlt = Color.FromArgb(255, 0x26, 0x22, 0x42),
            Border = Color.FromArgb(255, 0x45, 0x3F, 0x6B),
            Accent = Color.FromArgb(255, 0xA7, 0x8B, 0xFA),
            AccentHover = Color.FromArgb(255, 0xC4, 0xB5, 0xFD),
            TextPrimary = Color.FromArgb(255, 0xF5, 0xF3, 0xFF),
            TextSecondary = Color.FromArgb(255, 0xA5, 0x9B, 0xC8),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x22, 0x1E, 0x38),
            TilePrimary = Color.FromArgb(255, 0x26, 0x22, 0x42),
            TilePrimaryHover = Color.FromArgb(255, 0x36, 0x32, 0x5A),
            TileDangerBack = Color.FromArgb(255, 0x4C, 0x05, 0x19),
            TileDangerBackHover = Color.FromArgb(255, 0x88, 0x13, 0x37),
            TileDangerForeground = Color.FromArgb(255, 0xFD, 0xA4, 0xAF),
            DrinkRegular = Color.FromArgb(255, 0x1A, 0x16, 0x2E),
            DrinkOrderIn = Color.FromArgb(255, 0x8B, 0x5C, 0xF6),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xC4, 0xB5, 0xFD),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0x8B, 0x82, 0xB5)
        };

        private static readonly ThemePalette SunsetPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x18, 0x10, 0x12),
            Card = Color.FromArgb(255, 0x28, 0x1A, 0x1E),
            CardAlt = Color.FromArgb(255, 0x38, 0x26, 0x2C),
            Border = Color.FromArgb(255, 0x58, 0x40, 0x48),
            Accent = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            AccentHover = Color.FromArgb(255, 0xFB, 0x92, 0x3C),
            TextPrimary = Color.FromArgb(255, 0xFF, 0xF7, 0xED),
            TextSecondary = Color.FromArgb(255, 0xCA, 0xA4, 0xA0),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x32, 0x22, 0x28),
            TilePrimary = Color.FromArgb(255, 0x38, 0x26, 0x2C),
            TilePrimaryHover = Color.FromArgb(255, 0x4A, 0x34, 0x3C),
            TileDangerBack = Color.FromArgb(255, 0x4C, 0x05, 0x19),
            TileDangerBackHover = Color.FromArgb(255, 0x88, 0x13, 0x37),
            TileDangerForeground = Color.FromArgb(255, 0xFD, 0xA4, 0xAF),
            DrinkRegular = Color.FromArgb(255, 0x28, 0x1A, 0x1E),
            DrinkOrderIn = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xFB, 0x92, 0x3C),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0x78, 0x58, 0x60)
        };

        private static readonly ThemePalette NordPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xEC, 0xEF, 0xF4),
            Card = Color.FromArgb(255, 0xE5, 0xE9, 0xF0),
            CardAlt = Color.FromArgb(255, 0xD8, 0xDE, 0xE9),
            Border = Color.FromArgb(255, 0x4C, 0x56, 0x6A),
            Accent = Color.FromArgb(255, 0x5E, 0x81, 0xAC),
            AccentHover = Color.FromArgb(255, 0x81, 0xA1, 0xC1),
            TextPrimary = Color.FromArgb(255, 0x2E, 0x34, 0x40),
            TextSecondary = Color.FromArgb(255, 0x4C, 0x56, 0x6A),
            Success = Color.FromArgb(255, 0xA3, 0xBE, 0x8C),
            Danger = Color.FromArgb(255, 0xBF, 0x61, 0x6A),
            Warning = Color.FromArgb(255, 0xEB, 0xCB, 0x8B),
            RowAlt = Color.FromArgb(255, 0xE5, 0xE9, 0xF0),
            TilePrimary = Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
            TilePrimaryHover = Color.FromArgb(255, 0xD8, 0xDE, 0xE9),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0xF5, 0xF7, 0xFA),
            DrinkOrderIn = Color.FromArgb(255, 0x5E, 0x81, 0xAC),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0x5E, 0x81, 0xAC),
            PressedRing = Color.FromArgb(255, 0xBF, 0x61, 0x6A),
            ControlStrongBorder = Color.FromArgb(255, 0x3B, 0x42, 0x52)
        };

        private static readonly ThemePalette EmberPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x0C, 0x0A, 0x09),
            Card = Color.FromArgb(255, 0x1C, 0x19, 0x17),
            CardAlt = Color.FromArgb(255, 0x29, 0x25, 0x24),
            Border = Color.FromArgb(255, 0x44, 0x40, 0x3C),
            Accent = Color.FromArgb(255, 0xEA, 0x58, 0x0C),
            AccentHover = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            TextPrimary = Color.FromArgb(255, 0xFA, 0xFA, 0xF9),
            TextSecondary = Color.FromArgb(255, 0xA8, 0xA2, 0x9E),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x23, 0x20, 0x1F),
            TilePrimary = Color.FromArgb(255, 0x29, 0x25, 0x24),
            TilePrimaryHover = Color.FromArgb(255, 0x3F, 0x3A, 0x36),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x1C, 0x19, 0x17),
            DrinkOrderIn = Color.FromArgb(255, 0xEA, 0x58, 0x0C),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            PressedRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            ControlStrongBorder = Color.FromArgb(255, 0x57, 0x52, 0x4E)
        };

        private static readonly ThemePalette RosePalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xFF, 0xF1, 0xF2),
            Card = Color.FromArgb(255, 255, 255, 255),
            CardAlt = Color.FromArgb(255, 0xFF, 0xE4, 0xE6),
            Border = Color.FromArgb(255, 0xFE, 0xCD, 0xD3),
            Accent = Color.FromArgb(255, 0xE1, 0x1D, 0x48),
            AccentHover = Color.FromArgb(255, 0xF4, 0x3F, 0x5E),
            TextPrimary = Color.FromArgb(255, 0x88, 0x13, 0x37),
            TextSecondary = Color.FromArgb(255, 0x9F, 0x12, 0x39),
            Success = Color.FromArgb(255, 0x16, 0x83, 0x39),
            Danger = Color.FromArgb(255, 0xBE, 0x12, 0x3C),
            Warning = Color.FromArgb(255, 0xCA, 0x8A, 0x04),
            RowAlt = Color.FromArgb(255, 0xFF, 0xF1, 0xF2),
            TilePrimary = Color.FromArgb(255, 255, 255, 255),
            TilePrimaryHover = Color.FromArgb(255, 0xFF, 0xE4, 0xE6),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0xFF, 0xFB, 0xFC),
            DrinkOrderIn = Color.FromArgb(255, 0xE1, 0x1D, 0x48),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0xF4, 0x3F, 0x5E),
            PressedRing = Color.FromArgb(255, 0xBE, 0x12, 0x3C),
            ControlStrongBorder = Color.FromArgb(255, 0x88, 0x13, 0x37)
        };

        private static readonly ThemePalette LavenderPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xF5, 0xF3, 0xFF),
            Card = Color.FromArgb(255, 255, 255, 255),
            CardAlt = Color.FromArgb(255, 0xED, 0xE9, 0xFE),
            Border = Color.FromArgb(255, 0xC4, 0xB5, 0xFD),
            Accent = Color.FromArgb(255, 0x7C, 0x3A, 0xED),
            AccentHover = Color.FromArgb(255, 0x8B, 0x5C, 0xF6),
            TextPrimary = Color.FromArgb(255, 0x1E, 0x1B, 0x4B),
            TextSecondary = Color.FromArgb(255, 0x5B, 0x21, 0xB6),
            Success = Color.FromArgb(255, 0x16, 0x83, 0x39),
            Danger = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            Warning = Color.FromArgb(255, 0xCA, 0x8A, 0x04),
            RowAlt = Color.FromArgb(255, 0xF5, 0xF3, 0xFF),
            TilePrimary = Color.FromArgb(255, 255, 255, 255),
            TilePrimaryHover = Color.FromArgb(255, 0xED, 0xE9, 0xFE),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0xFA, 0xF5, 0xFF),
            DrinkOrderIn = Color.FromArgb(255, 0x7C, 0x3A, 0xED),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0x8B, 0x5C, 0xF6),
            PressedRing = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            ControlStrongBorder = Color.FromArgb(255, 0x31, 0x2E, 0x81)
        };

        private static readonly ThemePalette MintPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xF0, 0xFD, 0xF4),
            Card = Color.FromArgb(255, 255, 255, 255),
            CardAlt = Color.FromArgb(255, 0xDC, 0xFC, 0xE7),
            Border = Color.FromArgb(255, 0x86, 0xEF, 0xAC),
            Accent = Color.FromArgb(255, 0x05, 0x96, 0x69),
            AccentHover = Color.FromArgb(255, 0x10, 0xB9, 0x81),
            TextPrimary = Color.FromArgb(255, 0x06, 0x4E, 0x3B),
            TextSecondary = Color.FromArgb(255, 0x04, 0x78, 0x57),
            Success = Color.FromArgb(255, 0x16, 0x83, 0x39),
            Danger = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            Warning = Color.FromArgb(255, 0xCA, 0x8A, 0x04),
            RowAlt = Color.FromArgb(255, 0xEC, 0xFD, 0xF5),
            TilePrimary = Color.FromArgb(255, 255, 255, 255),
            TilePrimaryHover = Color.FromArgb(255, 0xDC, 0xFC, 0xE7),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0xF7, 0xFE, 0xF7),
            DrinkOrderIn = Color.FromArgb(255, 0x05, 0x96, 0x69),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0x10, 0xB9, 0x81),
            PressedRing = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            ControlStrongBorder = Color.FromArgb(255, 0x06, 0x4E, 0x3B)
        };

        private static readonly ThemePalette CoffeePalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x1C, 0x19, 0x17),
            Card = Color.FromArgb(255, 0x29, 0x25, 0x24),
            CardAlt = Color.FromArgb(255, 0x44, 0x40, 0x3C),
            Border = Color.FromArgb(255, 0x78, 0x71, 0x6C),
            Accent = Color.FromArgb(255, 0xD9, 0x77, 0x06),
            AccentHover = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            TextPrimary = Color.FromArgb(255, 0xFA, 0xFA, 0xF9),
            TextSecondary = Color.FromArgb(255, 0xA8, 0xA2, 0x9E),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x33, 0x2E, 0x2C),
            TilePrimary = Color.FromArgb(255, 0x44, 0x40, 0x3C),
            TilePrimaryHover = Color.FromArgb(255, 0x57, 0x52, 0x4E),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x29, 0x25, 0x24),
            DrinkOrderIn = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            PressedRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            ControlStrongBorder = Color.FromArgb(255, 0xA8, 0xA2, 0x9E)
        };

        private static readonly ThemePalette ArcticPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xF8, 0xFA, 0xFC),
            Card = Color.FromArgb(255, 255, 255, 255),
            CardAlt = Color.FromArgb(255, 0xE2, 0xE8, 0xF0),
            Border = Color.FromArgb(255, 0x94, 0xA3, 0xB8),
            Accent = Color.FromArgb(255, 0x0E, 0xA5, 0xE9),
            AccentHover = Color.FromArgb(255, 0x38, 0xBD, 0xF8),
            TextPrimary = Color.FromArgb(255, 0x0F, 0x17, 0x2A),
            TextSecondary = Color.FromArgb(255, 0x64, 0x74, 0x8B),
            Success = Color.FromArgb(255, 0x16, 0x83, 0x39),
            Danger = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            Warning = Color.FromArgb(255, 0xCA, 0x8A, 0x04),
            RowAlt = Color.FromArgb(255, 0xF1, 0xF5, 0xF9),
            TilePrimary = Color.FromArgb(255, 255, 255, 255),
            TilePrimaryHover = Color.FromArgb(255, 0xE2, 0xE8, 0xF0),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0xF8, 0xFA, 0xFC),
            DrinkOrderIn = Color.FromArgb(255, 0x0E, 0xA5, 0xE9),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0x38, 0xBD, 0xF8),
            PressedRing = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            ControlStrongBorder = Color.FromArgb(255, 0x33, 0x41, 0x55)
        };

        private static readonly ThemePalette GrapePalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x12, 0x07, 0x1A),
            Card = Color.FromArgb(255, 0x22, 0x0E, 0x2E),
            CardAlt = Color.FromArgb(255, 0x35, 0x15, 0x45),
            Border = Color.FromArgb(255, 0x6B, 0x21, 0x8A),
            Accent = Color.FromArgb(255, 0xE8, 0x79, 0xF9),
            AccentHover = Color.FromArgb(255, 0xF0, 0xAB, 0xFC),
            TextPrimary = Color.FromArgb(255, 0xFA, 0xE8, 0xFF),
            TextSecondary = Color.FromArgb(255, 0xD8, 0xB4, 0xFE),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x2A, 0x12, 0x38),
            TilePrimary = Color.FromArgb(255, 0x35, 0x15, 0x45),
            TilePrimaryHover = Color.FromArgb(255, 0x4A, 0x1D, 0x5C),
            TileDangerBack = Color.FromArgb(255, 0x4C, 0x05, 0x19),
            TileDangerBackHover = Color.FromArgb(255, 0x88, 0x13, 0x37),
            TileDangerForeground = Color.FromArgb(255, 0xFD, 0xA4, 0xAF),
            DrinkRegular = Color.FromArgb(255, 0x22, 0x0E, 0x2E),
            DrinkOrderIn = Color.FromArgb(255, 0xE8, 0x79, 0xF9),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xF0, 0xAB, 0xFC),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0xC0, 0x84, 0xFC)
        };

        private static readonly ThemePalette OutbackPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x2C, 0x24, 0x1A),
            Card = Color.FromArgb(255, 0x3D, 0x34, 0x28),
            CardAlt = Color.FromArgb(255, 0x4F, 0x44, 0x36),
            Border = Color.FromArgb(255, 0x7A, 0x6A, 0x56),
            Accent = Color.FromArgb(255, 0xC4, 0x5C, 0x3E),
            AccentHover = Color.FromArgb(255, 0xE0, 0x7A, 0x52),
            TextPrimary = Color.FromArgb(255, 0xFA, 0xF3, 0xE8),
            TextSecondary = Color.FromArgb(255, 0xC4, 0xB5, 0xA0),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x36, 0x2E, 0x24),
            TilePrimary = Color.FromArgb(255, 0x4F, 0x44, 0x36),
            TilePrimaryHover = Color.FromArgb(255, 0x62, 0x55, 0x44),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x3D, 0x34, 0x28),
            DrinkOrderIn = Color.FromArgb(255, 0xD9, 0x7D, 0x4A),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xE0, 0x7A, 0x52),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0x9A, 0x8A, 0x72)
        };

        private static readonly ThemePalette IronOrePalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x1A, 0x17, 0x15),
            Card = Color.FromArgb(255, 0x2A, 0x25, 0x22),
            CardAlt = Color.FromArgb(255, 0x3A, 0x34, 0x30),
            Border = Color.FromArgb(255, 0x57, 0x53, 0x4E),
            Accent = Color.FromArgb(255, 0xB4, 0x53, 0x09),
            AccentHover = Color.FromArgb(255, 0xD9, 0x77, 0x06),
            TextPrimary = Color.FromArgb(255, 0xF5, 0xF5, 0xF4),
            TextSecondary = Color.FromArgb(255, 0xA8, 0xA2, 0x9E),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x32, 0x2C, 0x28),
            TilePrimary = Color.FromArgb(255, 0x3A, 0x34, 0x30),
            TilePrimaryHover = Color.FromArgb(255, 0x4A, 0x44, 0x3E),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x2A, 0x25, 0x22),
            DrinkOrderIn = Color.FromArgb(255, 0xD9, 0x77, 0x06),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            PressedRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            ControlStrongBorder = Color.FromArgb(255, 0x78, 0x71, 0x6C)
        };

        private static readonly ThemePalette NeonNightPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x0A, 0x0A, 0x0F),
            Card = Color.FromArgb(255, 0x12, 0x12, 0x1A),
            CardAlt = Color.FromArgb(255, 0x1C, 0x1C, 0x28),
            Border = Color.FromArgb(255, 0x3A, 0x3A, 0x52),
            Accent = Color.FromArgb(255, 0x22, 0xD3, 0xEE),
            AccentHover = Color.FromArgb(255, 0x67, 0xE8, 0xF9),
            TextPrimary = Color.FromArgb(255, 0xF0, 0xFD, 0xFF),
            TextSecondary = Color.FromArgb(255, 0x94, 0xA3, 0xB8),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x18, 0x18, 0x24),
            TilePrimary = Color.FromArgb(255, 0x1C, 0x1C, 0x28),
            TilePrimaryHover = Color.FromArgb(255, 0x2A, 0x2A, 0x3C),
            TileDangerBack = Color.FromArgb(255, 0x4C, 0x05, 0x19),
            TileDangerBackHover = Color.FromArgb(255, 0x88, 0x13, 0x37),
            TileDangerForeground = Color.FromArgb(255, 0xFD, 0xA4, 0xAF),
            DrinkRegular = Color.FromArgb(255, 0x12, 0x12, 0x1A),
            DrinkOrderIn = Color.FromArgb(255, 0xE8, 0x79, 0xF9),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0x67, 0xE8, 0xF9),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0x5B, 0x5B, 0x78)
        };

        private static readonly ThemePalette TerminalGreenPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x0D, 0x11, 0x10),
            Card = Color.FromArgb(255, 0x14, 0x1A, 0x18),
            CardAlt = Color.FromArgb(255, 0x1C, 0x28, 0x22),
            Border = Color.FromArgb(255, 0x2D, 0x4A, 0x38),
            Accent = Color.FromArgb(255, 0x39, 0xFF, 0x14),
            AccentHover = Color.FromArgb(255, 0x6E, 0xFF, 0x6B),
            TextPrimary = Color.FromArgb(255, 0xC8, 0xF0, 0xC8),
            TextSecondary = Color.FromArgb(255, 0x6B, 0x9E, 0x7A),
            Success = Color.FromArgb(255, 0x39, 0xFF, 0x14),
            Danger = Color.FromArgb(255, 0xFF, 0x6B, 0x6B),
            Warning = Color.FromArgb(255, 0xFF, 0xE0, 0x66),
            RowAlt = Color.FromArgb(255, 0x16, 0x22, 0x1C),
            TilePrimary = Color.FromArgb(255, 0x1C, 0x28, 0x22),
            TilePrimaryHover = Color.FromArgb(255, 0x26, 0x38, 0x30),
            TileDangerBack = Color.FromArgb(255, 0x3D, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x6B, 0x1A, 0x1A),
            TileDangerForeground = Color.FromArgb(255, 0xFF, 0xB4, 0xB4),
            DrinkRegular = Color.FromArgb(255, 0x14, 0x1A, 0x18),
            DrinkOrderIn = Color.FromArgb(255, 0x22, 0xC5, 0x5E),
            DrinkSpecial = Color.FromArgb(255, 0xE8, 0xFF, 0x99),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x2D, 0x50, 0x1A),
            FocusRing = Color.FromArgb(255, 0x6E, 0xFF, 0x6B),
            PressedRing = Color.FromArgb(255, 0xFF, 0x6B, 0x6B),
            ControlStrongBorder = Color.FromArgb(255, 0x3D, 0x6B, 0x52)
        };

        private static readonly ThemePalette DesertSunsetPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x2A, 0x18, 0x12),
            Card = Color.FromArgb(255, 0x3D, 0x26, 0x1C),
            CardAlt = Color.FromArgb(255, 0x52, 0x34, 0x26),
            Border = Color.FromArgb(255, 0x7C, 0x4A, 0x36),
            Accent = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            AccentHover = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            TextPrimary = Color.FromArgb(255, 0xFF, 0xF1, 0xEC),
            TextSecondary = Color.FromArgb(255, 0xE8, 0xA8, 0x90),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x36, 0x22, 0x1A),
            TilePrimary = Color.FromArgb(255, 0x52, 0x34, 0x26),
            TilePrimaryHover = Color.FromArgb(255, 0x64, 0x42, 0x32),
            TileDangerBack = Color.FromArgb(255, 0x4C, 0x05, 0x19),
            TileDangerBackHover = Color.FromArgb(255, 0x88, 0x13, 0x37),
            TileDangerForeground = Color.FromArgb(255, 0xFD, 0xA4, 0xAF),
            DrinkRegular = Color.FromArgb(255, 0x3D, 0x26, 0x1C),
            DrinkOrderIn = Color.FromArgb(255, 0xF9, 0x73, 0x16),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0xA8, 0x6A, 0x52)
        };

        private static readonly ThemePalette SteelBluePalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x1C, 0x24, 0x30),
            Card = Color.FromArgb(255, 0x2A, 0x35, 0x45),
            CardAlt = Color.FromArgb(255, 0x38, 0x47, 0x5A),
            Border = Color.FromArgb(255, 0x47, 0x55, 0x69),
            Accent = Color.FromArgb(255, 0x60, 0xA5, 0xFA),
            AccentHover = Color.FromArgb(255, 0x93, 0xC5, 0xFD),
            TextPrimary = Color.FromArgb(255, 0xF1, 0xF5, 0xF9),
            TextSecondary = Color.FromArgb(255, 0x94, 0xA3, 0xB8),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x26, 0x32, 0x40),
            TilePrimary = Color.FromArgb(255, 0x38, 0x47, 0x5A),
            TilePrimaryHover = Color.FromArgb(255, 0x4A, 0x5D, 0x74),
            TileDangerBack = Color.FromArgb(255, 0x4C, 0x05, 0x19),
            TileDangerBackHover = Color.FromArgb(255, 0x88, 0x13, 0x37),
            TileDangerForeground = Color.FromArgb(255, 0xFD, 0xA4, 0xAF),
            DrinkRegular = Color.FromArgb(255, 0x2A, 0x35, 0x45),
            DrinkOrderIn = Color.FromArgb(255, 0x38, 0xBD, 0xF8),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0x93, 0xC5, 0xFD),
            PressedRing = Color.FromArgb(255, 0xFB, 0x71, 0x85),
            ControlStrongBorder = Color.FromArgb(255, 0x64, 0x74, 0x8B)
        };

        private static readonly ThemePalette FlounderersRedPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x0F, 0x0F, 0x0F),
            Card = Color.FromArgb(255, 0x1A, 0x1A, 0x1A),
            CardAlt = Color.FromArgb(255, 0x26, 0x26, 0x26),
            Border = Color.FromArgb(255, 0x40, 0x40, 0x40),
            Accent = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            AccentHover = Color.FromArgb(255, 0xEF, 0x44, 0x44),
            TextPrimary = Color.FromArgb(255, 0xFA, 0xFA, 0xFA),
            TextSecondary = Color.FromArgb(255, 0xA3, 0xA3, 0xA3),
            Success = Color.FromArgb(255, 0x22, 0xC5, 0x5E),
            Danger = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x22, 0x22, 0x22),
            TilePrimary = Color.FromArgb(255, 0x26, 0x26, 0x26),
            TilePrimaryHover = Color.FromArgb(255, 0x36, 0x36, 0x36),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x1A, 0x1A, 0x1A),
            DrinkOrderIn = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xEF, 0x44, 0x44),
            PressedRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            ControlStrongBorder = Color.FromArgb(255, 0x52, 0x52, 0x52)
        };

        private static readonly ThemePalette PitstopDarkPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x11, 0x11, 0x11),
            Card = Color.FromArgb(255, 0x1E, 0x1E, 0x1E),
            CardAlt = Color.FromArgb(255, 0x2A, 0x2A, 0x2A),
            Border = Color.FromArgb(255, 0x44, 0x44, 0x44),
            Accent = Color.FromArgb(255, 0xF5, 0x9E, 0x0B),
            AccentHover = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            TextPrimary = Color.FromArgb(255, 0xFA, 0xFA, 0xF9),
            TextSecondary = Color.FromArgb(255, 0xA8, 0xA2, 0x9E),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x24, 0x24, 0x24),
            TilePrimary = Color.FromArgb(255, 0x2A, 0x2A, 0x2A),
            TilePrimaryHover = Color.FromArgb(255, 0x3A, 0x3A, 0x3A),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x1E, 0x1E, 0x1E),
            DrinkOrderIn = Color.FromArgb(255, 0xF5, 0x9E, 0x0B),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            PressedRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            ControlStrongBorder = Color.FromArgb(255, 0x57, 0x57, 0x57)
        };

        private static readonly ThemePalette WorkshopPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x2A, 0x2A, 0x28),
            Card = Color.FromArgb(255, 0x3D, 0x3D, 0x38),
            CardAlt = Color.FromArgb(255, 0x4F, 0x4F, 0x48),
            Border = Color.FromArgb(255, 0x52, 0x52, 0x4E),
            Accent = Color.FromArgb(255, 0xEA, 0xB3, 0x08),
            AccentHover = Color.FromArgb(255, 0xFA, 0xCC, 0x15),
            TextPrimary = Color.FromArgb(255, 0xFA, 0xFA, 0xF9),
            TextSecondary = Color.FromArgb(255, 0xA8, 0xA2, 0x9E),
            Success = Color.FromArgb(255, 0x34, 0xD3, 0x99),
            Danger = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            Warning = Color.FromArgb(255, 0xEA, 0xB3, 0x08),
            RowAlt = Color.FromArgb(255, 0x34, 0x34, 0x30),
            TilePrimary = Color.FromArgb(255, 0x4F, 0x4F, 0x48),
            TilePrimaryHover = Color.FromArgb(255, 0x5E, 0x5E, 0x56),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x3D, 0x3D, 0x38),
            DrinkOrderIn = Color.FromArgb(255, 0xEA, 0xB3, 0x08),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xFA, 0xCC, 0x15),
            PressedRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            ControlStrongBorder = Color.FromArgb(255, 0x6B, 0x6A, 0x64)
        };

        private static readonly ThemePalette TrackDayPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0x0A, 0x0A, 0x0A),
            Card = Color.FromArgb(255, 0x17, 0x17, 0x17),
            CardAlt = Color.FromArgb(255, 0x24, 0x24, 0x24),
            Border = Color.FromArgb(255, 0x40, 0x40, 0x40),
            Accent = Color.FromArgb(255, 0xEF, 0x44, 0x44),
            AccentHover = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            TextPrimary = Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
            TextSecondary = Color.FromArgb(255, 0xA3, 0xA3, 0xA3),
            Success = Color.FromArgb(255, 0x22, 0xC5, 0x5E),
            Danger = Color.FromArgb(255, 0xEF, 0x44, 0x44),
            Warning = Color.FromArgb(255, 0xFB, 0xBF, 0x24),
            RowAlt = Color.FromArgb(255, 0x1C, 0x1C, 0x1C),
            TilePrimary = Color.FromArgb(255, 0x24, 0x24, 0x24),
            TilePrimaryHover = Color.FromArgb(255, 0x33, 0x33, 0x33),
            TileDangerBack = Color.FromArgb(255, 0x45, 0x0A, 0x0A),
            TileDangerBackHover = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0x17, 0x17, 0x17),
            DrinkOrderIn = Color.FromArgb(255, 0xEF, 0x44, 0x44),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x78, 0x35, 0x0F),
            FocusRing = Color.FromArgb(255, 0xF8, 0x71, 0x71),
            PressedRing = Color.FromArgb(255, 0xDC, 0x26, 0x26),
            ControlStrongBorder = Color.FromArgb(255, 0x52, 0x52, 0x52)
        };

        private static readonly ThemePalette CreamPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xFA, 0xF8, 0xF5),
            Card = Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
            CardAlt = Color.FromArgb(255, 0xF3, 0xEE, 0xE8),
            Border = Color.FromArgb(255, 0xE0, 0xD8, 0xCE),
            Accent = Color.FromArgb(255, 0x8B, 0x73, 0x55),
            AccentHover = Color.FromArgb(255, 0xA6, 0x8A, 0x6A),
            TextPrimary = Color.FromArgb(255, 0x2C, 0x28, 0x24),
            TextSecondary = Color.FromArgb(255, 0x78, 0x71, 0x6A),
            Success = Color.FromArgb(255, 0x16, 0x83, 0x39),
            Danger = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            Warning = Color.FromArgb(255, 0xCA, 0x8A, 0x04),
            RowAlt = Color.FromArgb(255, 0xF6, 0xF2, 0xEC),
            TilePrimary = Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
            TilePrimaryHover = Color.FromArgb(255, 0xF3, 0xEE, 0xE8),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0xFF, 0xFD, 0xFA),
            DrinkOrderIn = Color.FromArgb(255, 0x8B, 0x73, 0x55),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0xA6, 0x8A, 0x6A),
            PressedRing = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            ControlStrongBorder = Color.FromArgb(255, 0x44, 0x40, 0x3C)
        };

        private static readonly ThemePalette EucalyptusPalette = new ThemePalette
        {
            Background = Color.FromArgb(255, 0xE8, 0xEE, 0xEA),
            Card = Color.FromArgb(255, 0xF4, 0xF7, 0xF5),
            CardAlt = Color.FromArgb(255, 0xE0, 0xEB, 0xE5),
            Border = Color.FromArgb(255, 0xB8, 0xC9, 0xC0),
            Accent = Color.FromArgb(255, 0x5F, 0x85, 0x75),
            AccentHover = Color.FromArgb(255, 0x7A, 0xA8, 0x95),
            TextPrimary = Color.FromArgb(255, 0x2D, 0x3D, 0x36),
            TextSecondary = Color.FromArgb(255, 0x5C, 0x6D, 0x65),
            Success = Color.FromArgb(255, 0x16, 0x83, 0x39),
            Danger = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            Warning = Color.FromArgb(255, 0xCA, 0x8A, 0x04),
            RowAlt = Color.FromArgb(255, 0xEC, 0xF2, 0xEE),
            TilePrimary = Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
            TilePrimaryHover = Color.FromArgb(255, 0xE0, 0xEB, 0xE5),
            TileDangerBack = Color.FromArgb(255, 0x7F, 0x1D, 0x1D),
            TileDangerBackHover = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            TileDangerForeground = Color.FromArgb(255, 0xFC, 0xA5, 0xA5),
            DrinkRegular = Color.FromArgb(255, 0xF8, 0xFB, 0xF9),
            DrinkOrderIn = Color.FromArgb(255, 0x5F, 0x85, 0x75),
            DrinkSpecial = Color.FromArgb(255, 0xFD, 0xE6, 0x8A),
            DrinkSpecialPriceText = Color.FromArgb(255, 0x92, 0x40, 0x0E),
            FocusRing = Color.FromArgb(255, 0x7A, 0xA8, 0x95),
            PressedRing = Color.FromArgb(255, 0xB9, 0x1C, 0x1C),
            ControlStrongBorder = Color.FromArgb(255, 0x3D, 0x52, 0x48)
        };

        private static ThemePalette PaletteFor(UiThemeId id) => id switch
        {
            UiThemeId.Dark => DarkPalette,
            UiThemeId.Slate => SlatePalette,
            UiThemeId.Warm => WarmPalette,
            UiThemeId.Ocean => OceanPalette,
            UiThemeId.Forest => ForestPalette,
            UiThemeId.Midnight => MidnightPalette,
            UiThemeId.Sunset => SunsetPalette,
            UiThemeId.Nord => NordPalette,
            UiThemeId.Ember => EmberPalette,
            UiThemeId.Rose => RosePalette,
            UiThemeId.Lavender => LavenderPalette,
            UiThemeId.Mint => MintPalette,
            UiThemeId.Coffee => CoffeePalette,
            UiThemeId.Arctic => ArcticPalette,
            UiThemeId.Grape => GrapePalette,
            UiThemeId.Outback => OutbackPalette,
            UiThemeId.IronOre => IronOrePalette,
            UiThemeId.NeonNight => NeonNightPalette,
            UiThemeId.TerminalGreen => TerminalGreenPalette,
            UiThemeId.DesertSunset => DesertSunsetPalette,
            UiThemeId.SteelBlue => SteelBluePalette,
            UiThemeId.FlounderersRed => FlounderersRedPalette,
            UiThemeId.PitstopDark => PitstopDarkPalette,
            UiThemeId.Workshop => WorkshopPalette,
            UiThemeId.TrackDay => TrackDayPalette,
            UiThemeId.Cream => CreamPalette,
            UiThemeId.Eucalyptus => EucalyptusPalette,
            _ => LightPalette
        };

        // Must be initialized after LightPalette (and peers) - declaring _palette above LightPalette leaves it null.
        private static ThemePalette _palette = LightPalette;

        public static UiThemeId CurrentThemeId { get; private set; } = UiThemeId.Light;

        /// <summary>Fired after the active palette changes (login, user settings, or preview). UI should refresh chrome.</summary>
        public static event EventHandler ThemeChanged;

        /// <summary>Stronger card shadow on dark-style chromes.</summary>
        public static bool UsesStrongCardShadow =>
            CurrentThemeId == UiThemeId.Dark
            || CurrentThemeId == UiThemeId.Slate
            || CurrentThemeId == UiThemeId.Ocean
            || CurrentThemeId == UiThemeId.Forest
            || CurrentThemeId == UiThemeId.Midnight
            || CurrentThemeId == UiThemeId.Sunset
            || CurrentThemeId == UiThemeId.Ember
            || CurrentThemeId == UiThemeId.Coffee
            || CurrentThemeId == UiThemeId.Grape
            || CurrentThemeId == UiThemeId.Outback
            || CurrentThemeId == UiThemeId.IronOre
            || CurrentThemeId == UiThemeId.NeonNight
            || CurrentThemeId == UiThemeId.TerminalGreen
            || CurrentThemeId == UiThemeId.DesertSunset
            || CurrentThemeId == UiThemeId.SteelBlue
            || CurrentThemeId == UiThemeId.FlounderersRed
            || CurrentThemeId == UiThemeId.PitstopDark
            || CurrentThemeId == UiThemeId.Workshop
            || CurrentThemeId == UiThemeId.TrackDay;

        public static Color Background => _palette.Background;
        public static Color Card => _palette.Card;
        public static Color CardAlt => _palette.CardAlt;
        public static Color Border => _palette.Border;
        public static Color Accent => _palette.Accent;
        public static Color AccentHover => _palette.AccentHover;
        public static Color TextPrimary => _palette.TextPrimary;
        public static Color TextSecondary => _palette.TextSecondary;
        public static Color Success => _palette.Success;
        public static Color Danger => _palette.Danger;
        public static Color Warning => _palette.Warning;
        public static Color RowAlt => _palette.RowAlt;

        public static Color TilePrimary => _palette.TilePrimary;
        public static Color TilePrimaryHover => _palette.TilePrimaryHover;
        public static Color TileDangerBack => _palette.TileDangerBack;
        public static Color TileDangerBackHover => _palette.TileDangerBackHover;
        public static Color TileDangerForeground => _palette.TileDangerForeground;
        public static Color DrinkRegular => _palette.DrinkRegular;
        public static Color DrinkOrderIn => _palette.DrinkOrderIn;
        public static Color DrinkSpecial => _palette.DrinkSpecial;
        public static Color DrinkSpecialPriceText => _palette.DrinkSpecialPriceText;
        public static Color FocusRing => _palette.FocusRing;
        public static Color PressedRing => _palette.PressedRing;
        public static Color ControlStrongBorder => _palette.ControlStrongBorder;

        /// <summary>Semi-transparent overlay from the current accent (category highlights, list selection).</summary>
        public static Color AccentOverlaySoft => Color.FromArgb(0x33, Accent.R, Accent.G, Accent.B);

        public static void Apply(UiThemeId id)
        {
            CurrentThemeId = id;
            _palette = PaletteFor(id);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Updates common input controls to match the active palette (text fields, lists, tabs, etc.).
        /// Does not restyle semantic <see cref="Button"/> variants or <see cref="DataGridView"/> rows.
        /// </summary>
    }
}

