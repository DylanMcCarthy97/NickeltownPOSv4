using System;
using Microsoft.UI.Xaml.Data;

namespace NickeltownPOSV4.Converters;

/// <summary>Maps Shift (bool) + single-letter ConverterParameter to that letter in upper or lower case for keyboard labels.</summary>
public sealed class LetterCaseConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var letter = parameter as string ?? "a";
        if (letter.Length == 0)
        {
            letter = "a";
        }

        var ch = letter[0];
        var shift = value is true;
        return shift ? char.ToUpperInvariant(ch).ToString() : char.ToLowerInvariant(ch).ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
