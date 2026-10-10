using System;
using System.IO;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace NickeltownPOSV4.Converters;

/// <summary>Builds a <see cref="BitmapImage"/> from a file path or absolute URI string (e.g. ms-appx:).</summary>
public sealed class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.Trim();
        var decodeWidth = 220;
        if (parameter is string p && int.TryParse(p.Trim(), out var parsed) && parsed > 0)
        {
            decodeWidth = parsed;
        }

        try
        {
            if (Uri.TryCreate(s, UriKind.Absolute, out var absolute))
            {
                return new BitmapImage(absolute) { DecodePixelWidth = decodeWidth };
            }

            var full = Path.GetFullPath(s);
            if (!File.Exists(full))
            {
                return null;
            }

            var fileUri = new Uri(full);
            return new BitmapImage(fileUri) { DecodePixelWidth = decodeWidth };
        }
        catch (UriFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
