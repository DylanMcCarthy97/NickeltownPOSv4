using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Themes;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Converters;

public sealed class TabSelectedBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var bathurst = value is TabsBoardCellViewModel { IsBathurstTheme: true }
                       || (value is true && BathurstTabsTheme.IsEnabled);

        var selected = value is TabsBoardCellViewModel { TabIsSelected: true } || value is true;

        if (bathurst)
        {
            return BathurstTabsBrushes.Token(selected ? "CardSelected" : "Card");
        }

        return LookupBrush(selected ? "PosTabCardSelectedFill" : "PosSurfaceBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Brush LookupBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.White);
    }
}

public sealed class TabSelectedBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is TabsBoardCellViewModel cell)
        {
            if (cell.IsBathurstTheme)
            {
                return cell.TabIsSelected
                    ? BathurstTabsBrushes.Token("SelectedOutline")
                    : BathurstTabsBrushes.Token("Border");
            }

            if (cell.TabIsSelected && cell.Tab is { } tab)
            {
                return TabCardStripBrushConverter.ResolveStripBrush(tab.IsGuest, tab.BalanceTier);
            }

            return LookupBrush("PosBorderBrush");
        }

        return LookupBrush("PosBorderBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Brush LookupBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }
}

public sealed class TabSelectedBorderThicknessConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? new Thickness(3) : new Thickness(1);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
