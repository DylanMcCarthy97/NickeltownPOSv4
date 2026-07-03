using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace NickeltownPOSV4.Converters;

/// <summary>Maps bool to a border brush (accent vs neutral).</summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var on = value is true;
        if (on)
        {
            var accent = Application.Current.Resources["PosAccentBrush"];
            if (accent is Brush accentBrush)
            {
                return accentBrush;
            }

            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 102, 217));
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 214, 222, 234));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Alternating list row background for stock browser.</summary>
public sealed class StockAlternateRowBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value is true ? "PosSurfaceAltBrush" : "PosSurfaceBrush";
        if (Application.Current.Resources[key] is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Parses #AARRGGBB strings for tab history entry type colors.</summary>
public sealed class ArgbStringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string hex || hex.Length < 7 || !hex.StartsWith('#'))
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 17, 24, 39));
        }

        try
        {
            var a = System.Convert.ToByte(hex.Substring(1, 2), 16);
            var r = System.Convert.ToByte(hex.Substring(3, 2), 16);
            var g = System.Convert.ToByte(hex.Substring(5, 2), 16);
            var b = System.Convert.ToByte(hex.Substring(7, 2), 16);
            return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
        }
        catch
        {
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 17, 24, 39));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
