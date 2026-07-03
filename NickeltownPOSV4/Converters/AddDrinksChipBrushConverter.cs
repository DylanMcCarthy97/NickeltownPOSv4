using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace NickeltownPOSV4.Converters;

/// <summary>Category chip fill: selected = accent blue, unselected = white surface.</summary>
public sealed class AddDrinksChipBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var selected = value is true;
        if (selected)
        {
            if (Application.Current.Resources["PosAccentBrush"] is Brush accent)
            {
                return accent;
            }

            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 102, 217));
        }

        return Application.Current.Resources["PosSurfaceBrush"] is Brush surface
            ? surface
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Category chip text: selected = white, unselected = primary text.</summary>
public sealed class AddDrinksChipForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var selected = value is true;
        if (selected)
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        }

        return Application.Current.Resources["PosTextPrimaryBrush"] is Brush text
            ? text
            : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 17, 24, 39));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
