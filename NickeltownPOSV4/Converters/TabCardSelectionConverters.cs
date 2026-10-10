using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Converters;

public sealed class TabSelectedBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true)
        {
            return TabsThemeBrushLookup.Resolve("PosTabCardSelectedFill");
        }

        return TabsThemeBrushLookup.Resolve("PosSurfaceBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class TabSelectedBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TabsBoardCellViewModel { TabIsSelected: true, Tab: { } tab })
        {
            if (TabsThemeBrushLookup.IsBathurstActive)
            {
                return TabsThemeBrushLookup.Resolve("PosSelectedCardBorderBrush");
            }

            return TabCardStripBrushConverter.ResolveStripBrush(tab.IsGuest, tab.BalanceTier);
        }

        return TabsThemeBrushLookup.Resolve("PosBorderBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class TabSelectedBorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true)
        {
            return TabsThemeBrushLookup.IsBathurstActive ? new Thickness(2) : new Thickness(3);
        }

        return new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
