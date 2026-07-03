using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace NickeltownPOSV4.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var mode = parameter as string;
        var invert = string.Equals(mode, "Invert", StringComparison.OrdinalIgnoreCase);
        var notEmpty = string.Equals(mode, "NotEmpty", StringComparison.OrdinalIgnoreCase);

        bool flag;
        if (notEmpty)
        {
            flag = value switch
            {
                string s => !string.IsNullOrWhiteSpace(s),
                System.Collections.ICollection c => c.Count > 0,
                null => false,
                _ => true,
            };
        }
        else
        {
            flag = value is true;
        }

        if (invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
