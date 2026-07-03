using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Converters;

public sealed class TabsModeSegmentBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var selected = value is TabsBoardMode mode
                       && parameter is string key
                       && key switch
                       {
                           "Open" or "Member" => mode == TabsBoardMode.OpenTabs,
                           "Guest" => mode == TabsBoardMode.GuestTabs,
                           "Archived" => mode == TabsBoardMode.ArchivedTabs,
                           _ => false,
                       };

        var brushKey = selected ? "PosModeSegmentSelectedBrush" : "PosModeSegmentIdleBrush";
        if (Application.Current?.Resources.TryGetValue(brushKey, out var o) == true && o is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

public sealed class TabsModeSegmentForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var selected = value is TabsBoardMode mode
                       && parameter is string key
                       && key switch
                       {
                           "Open" or "Member" => mode == TabsBoardMode.OpenTabs,
                           "Guest" => mode == TabsBoardMode.GuestTabs,
                           "Archived" => mode == TabsBoardMode.ArchivedTabs,
                           _ => false,
                       };

        if (selected)
        {
            return Lookup("PosOnAccentForegroundBrush");
        }

        if (parameter is string p && p == "Guest")
        {
            return Lookup("PosGuestBrush");
        }

        return Lookup("PosTextPrimaryBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Brush Lookup(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }
}