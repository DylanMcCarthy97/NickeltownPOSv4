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
        // Bound IsBathurstTheme wins; do not OR the static flag or Race Day chrome sticks after disable.
        if (value is true)
        {
            return BathurstTabsBrushes.Token(token);
        }

        if (value is false)
        {
            return BathurstTabsBrushes.LookupPos(BathurstTabsBrushes.PosKey(token));
        }

        // Unbound / unexpected: fall back to installation preference.
        if (BathurstTabsTheme.IsEnabled)
        {
            return BathurstTabsBrushes.Token(token);
        }

        return BathurstTabsBrushes.LookupPos(BathurstTabsBrushes.PosKey(token));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
