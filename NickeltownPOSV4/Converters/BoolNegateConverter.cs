using System;
using Microsoft.UI.Xaml.Data;

namespace NickeltownPOSV4.Converters;

/// <summary>Inverts a boolean (for <c>IsEnabled</c> when bound to <c>IsBusy</c>, etc.).</summary>
public sealed class BoolNegateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is true;
        return !flag;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
