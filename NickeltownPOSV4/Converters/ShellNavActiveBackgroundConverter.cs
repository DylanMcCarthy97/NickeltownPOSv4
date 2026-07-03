using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Converters;

public sealed class ShellNavActiveBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var routeId = parameter as string;
        if (value is ShellRoute route
            && !string.IsNullOrEmpty(routeId)
            && string.Equals(route.Id, routeId, StringComparison.OrdinalIgnoreCase)
            && Application.Current.Resources.TryGetValue("PosFooterNavActiveBrush", out var brush)
            && brush is Brush activeBrush)
        {
            return activeBrush;
        }

        return Application.Current.Resources.TryGetValue("PosFooterNavIdleBrush", out var idle)
               && idle is Brush idleBrush
            ? idleBrush
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}