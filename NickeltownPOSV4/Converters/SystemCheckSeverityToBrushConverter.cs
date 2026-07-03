using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Converters;

public sealed class SystemCheckSeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            SystemCheckSeverity.Ok => ResolveBrush("PosSuccessBrush"),
            SystemCheckSeverity.Warning => ResolveBrush("PosWarningAmberBrush"),
            SystemCheckSeverity.Error => ResolveBrush("PosButtonDangerBrush"),
            _ => ResolveBrush("PosTextSecondaryBrush"),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Brush ResolveBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139));
    }
}
