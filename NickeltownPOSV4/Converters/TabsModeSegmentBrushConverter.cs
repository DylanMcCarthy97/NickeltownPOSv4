using System;
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
        return TabsThemeBrushLookup.Resolve(brushKey);
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
            return TabsThemeBrushLookup.Resolve("PosOnAccentForegroundBrush");
        }

        if (parameter is string p && p == "Guest")
        {
            return TabsThemeBrushLookup.Resolve("PosGuestBrush");
        }

        return TabsThemeBrushLookup.Resolve("PosTextPrimaryBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
