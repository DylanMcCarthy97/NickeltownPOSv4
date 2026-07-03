using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Converters;

public sealed class ShellNavActiveForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var routeId = parameter as string;
        var isActive = value is ShellRoute route
                       && !string.IsNullOrEmpty(routeId)
                       && string.Equals(route.Id, routeId, StringComparison.OrdinalIgnoreCase);
        var key = isActive ? "PosOnDarkForegroundBrush" : "PosOnDarkSecondaryForegroundBrush";
        if (Application.Current.Resources.TryGetValue(key, out var brush) && brush is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}