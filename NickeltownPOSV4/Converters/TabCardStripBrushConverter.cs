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
    public static Brush ResolveStripBrush(bool isGuest, TabBalanceTier tier) =>
        BathurstTabsTheme.IsEnabled
            ? BathurstTabsBrushes.FromArgb(BathurstTabsTheme.StripColor(isGuest, tier))
            : Lookup(isGuest ? "PosTabStripGuestBrush" : TabBalanceTierBrushes.StripResourceKey(tier));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TabsBoardCellViewModel cell)
        {
            var isGuest = cell.TabIsGuest;
            var cellTier = cell.DisplayBalanceTier;
            return cell.IsBathurstTheme || BathurstTabsTheme.IsEnabled
                ? BathurstTabsBrushes.FromArgb(BathurstTabsTheme.StripColor(isGuest, cellTier))
                : Lookup(isGuest ? "PosTabStripGuestBrush" : TabBalanceTierBrushes.StripResourceKey(cellTier));
        }

        if (value is TabCardModel tab)
        {
            return BathurstTabsTheme.IsEnabled
                ? BathurstTabsBrushes.FromArgb(BathurstTabsTheme.StripColor(tab.IsGuest, tab.BalanceTier))
                : Lookup(TabBalanceTierBrushes.StripResourceKey(tab));
        }

        if (value is TabBalanceTier balanceTier)
        {
            return BathurstTabsTheme.IsEnabled
                ? BathurstTabsBrushes.FromArgb(BathurstTabsTheme.BalanceColor(balanceTier))
                : Lookup(TabBalanceTierBrushes.StripResourceKey(balanceTier));
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