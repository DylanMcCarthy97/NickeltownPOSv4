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
        var (bathurst, selected) = ReadChrome(value);

        if (bathurst)
        {
            return BathurstTabsBrushes.Token(selected ? "CardSelected" : "Card");
        }

        return LookupBrush(selected ? "PosTabCardSelectedFill" : "PosSurfaceBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    internal static (bool Bathurst, bool Selected) ReadChrome(object value)
    {
        if (value is TabsBoardCellViewModel cell)
        {
            return (cell.IsBathurstTheme || BathurstTabsTheme.IsEnabled, cell.TabIsSelected);
        }

        if (value is string key && key.Length > 0)
        {
            var bathurst = key[0] == 'B' || BathurstTabsTheme.IsEnabled;
            var selected = key.Contains(":S:", StringComparison.Ordinal);
            return (bathurst, selected);
        }

        if (value is true)
        {
            return (BathurstTabsTheme.IsEnabled, true);
        }

        return (BathurstTabsTheme.IsEnabled, false);
    }

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
        var (bathurst, selected) = TabSelectedBackgroundConverter.ReadChrome(value);
        if (bathurst)
        {
            return selected
                ? BathurstTabsBrushes.Token("SelectedOutline")
                : BathurstTabsBrushes.Token("Border");
        }

        if (value is TabsBoardCellViewModel { TabIsSelected: true, Tab: { } tab })
        {
            return TabCardStripBrushConverter.ResolveStripBrush(tab.IsGuest, tab.BalanceTier);
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
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string key)
        {
            return key.Contains(":S:", StringComparison.Ordinal) ? new Thickness(3) : new Thickness(1);
        }

        return value is true ? new Thickness(3) : new Thickness(1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
