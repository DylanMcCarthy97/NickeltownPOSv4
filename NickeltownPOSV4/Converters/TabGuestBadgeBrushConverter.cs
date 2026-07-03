using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace NickeltownPOSV4.Converters;

public sealed class TabGuestBadgeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value is true ? "PosGuestBadgeFillBrush" : "PosMemberBadgeFillBrush";
        return LookupBrush(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Brush LookupBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}