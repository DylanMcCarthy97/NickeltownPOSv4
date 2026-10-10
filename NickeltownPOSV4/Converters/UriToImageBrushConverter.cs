using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace NickeltownPOSV4.Converters;

/// <summary>
/// Builds an <see cref="ImageBrush"/> from an ms-appx URI, using UniformToFill so
/// artwork is cropped to the host bounds without non-uniform stretching.
/// </summary>
public sealed class UriToImageBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string uri || string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
        {
            return null;
        }

        var alignY = AlignmentY.Center;
        if (parameter is string p)
        {
            if (p.Equals("Top", StringComparison.OrdinalIgnoreCase))
            {
                alignY = AlignmentY.Top;
            }
            else if (p.Equals("Bottom", StringComparison.OrdinalIgnoreCase))
            {
                alignY = AlignmentY.Bottom;
            }
        }

        return new ImageBrush
        {
            ImageSource = new BitmapImage(absolute),
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = alignY,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
