using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Converters;

/// <summary>Top accent strip on tab cards from balance/guest status.</summary>
public sealed class TabCardStripBrushConverter : IValueConverter
{
    public static Brush ResolveStripBrush(bool isGuest, TabBalanceTier tier) =>
        TabsThemeBrushLookup.Resolve(isGuest ? "PosTabStripGuestBrush" : TabBalanceTierBrushes.StripResourceKey(tier));

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TabCardModel tab)
        {
            return TabsThemeBrushLookup.Resolve(TabBalanceTierBrushes.StripResourceKey(tab));
        }

        if (value is TabBalanceTier tier)
        {
            return TabsThemeBrushLookup.Resolve(TabBalanceTierBrushes.StripResourceKey(tier));
        }

        return TabsThemeBrushLookup.Resolve(TabBalanceTierBrushes.StripResourceKey(TabBalanceTier.Settled));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
