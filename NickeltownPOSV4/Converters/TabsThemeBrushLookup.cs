using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Services.Tabs;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.Converters;

/// <summary>Resolves Pos* brushes for Bar Tabs, honouring the scoped Bathurst visual when active.</summary>
internal static class TabsThemeBrushLookup
{
    private static ITabsVisualThemeService? _theme;

    public static void Bind(ITabsVisualThemeService theme) => _theme = theme;

    public static bool IsBathurstActive => _theme?.IsBathurstVisualActive == true;

    public static Brush Resolve(string posKey)
    {
        if (IsBathurstActive && BathurstTabsPalette.TryGetBrush(posKey, out var bathurst))
        {
            return bathurst;
        }

        if (Application.Current?.Resources.TryGetValue(posKey, out var o) == true && o is Brush appBrush)
        {
            return appBrush;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }
}
