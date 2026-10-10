using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Themes;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Converters;

/// <summary>Top accent strip on tab cards from balance/guest status.</summary>
public sealed class TabCardStripBrushConverter : IValueConverter
{
    public static Brush ResolveStripBrush(bool isGuest, TabBalanceTier tier, bool? bathurst = null) =>
        (bathurst ?? BathurstTabsTheme.IsEnabled)
            ? BathurstTabsBrushes.FromArgb(BathurstTabsTheme.StripColor(isGuest, tier))
            : Lookup(isGuest ? "PosTabStripGuestBrush" : TabBalanceTierBrushes.StripResourceKey(tier));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TabsBoardCellViewModel cell)
        {
            return ResolveStripBrush(cell.TabIsGuest, cell.DisplayBalanceTier, cell.IsBathurstTheme);
        }

        if (value is TabCardModel tab)
        {
            return ResolveStripBrush(tab.IsGuest, tab.BalanceTier);
        }

        if (value is TabBalanceTier balanceTier)
        {
            return ResolveStripBrush(isGuest: false, balanceTier);
        }

        return Lookup(TabBalanceTierBrushes.StripResourceKey(TabBalanceTier.Settled));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Brush Lookup(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}
