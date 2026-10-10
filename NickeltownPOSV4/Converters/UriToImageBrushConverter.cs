using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace NickeltownPOSV4.Converters;

/// <summary>
/// Builds an <see cref="ImageBrush"/> from an ms-appx URI.
/// Default stretch is Uniform (letterbox) so card car art keeps natural proportions
/// inside a dark host. Pass ConverterParameter values like "UniformToFill",
/// "Uniform", and optional alignment "Top"/"Bottom"/"Center" (pipe or comma separated).
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

        var stretch = Stretch.Uniform;
        var alignX = AlignmentX.Center;
        var alignY = AlignmentY.Center;

        if (parameter is string raw && !string.IsNullOrWhiteSpace(raw))
        {
            foreach (var token in raw.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Equals("UniformToFill", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Fill", StringComparison.OrdinalIgnoreCase))
                {
                    stretch = token.Equals("Fill", StringComparison.OrdinalIgnoreCase)
                        ? Stretch.Fill
                        : Stretch.UniformToFill;
                }
                else if (token.Equals("Uniform", StringComparison.OrdinalIgnoreCase) ||
                         token.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    stretch = token.Equals("None", StringComparison.OrdinalIgnoreCase)
                        ? Stretch.None
                        : Stretch.Uniform;
                }
                else if (token.Equals("Top", StringComparison.OrdinalIgnoreCase))
                {
                    alignY = AlignmentY.Top;
                }
                else if (token.Equals("Bottom", StringComparison.OrdinalIgnoreCase))
                {
                    alignY = AlignmentY.Bottom;
                }
                else if (token.Equals("Left", StringComparison.OrdinalIgnoreCase))
                {
                    alignX = AlignmentX.Left;
                }
                else if (token.Equals("Right", StringComparison.OrdinalIgnoreCase))
                {
                    alignX = AlignmentX.Right;
                }
                else if (token.Equals("Center", StringComparison.OrdinalIgnoreCase))
                {
                    alignX = AlignmentX.Center;
                    alignY = AlignmentY.Center;
                }
            }
        }

        return new ImageBrush
        {
            ImageSource = new BitmapImage(absolute),
            Stretch = stretch,
            AlignmentX = alignX,
            AlignmentY = alignY,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
