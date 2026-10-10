using System;
using Microsoft.UI.Xaml.Data;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.Converters;

/// <summary>Resolves a Bar Tabs chrome token from the Bathurst palette or the active global theme.</summary>
public sealed class BathurstOrThemeBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var token = parameter as string ?? "Canvas";
        if (value is true || BathurstTabsTheme.IsEnabled)
        {
            return BathurstTabsBrushes.Token(token);
        }

        return BathurstTabsBrushes.LookupPos(BathurstTabsBrushes.PosKey(token));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
