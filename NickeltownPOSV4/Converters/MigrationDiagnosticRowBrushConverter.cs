using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models.Migration;
using Windows.UI;

namespace NickeltownPOSV4.Converters;

/// <summary>Background / border brush for migration preview rows (parse errors, zero-count tabular warnings, normal).</summary>
public sealed class MigrationDiagnosticRowBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var role = parameter as string ?? "Background";
        if (value is not MigrationFilePreviewDiagnostic d)
        {
            return Neutral(role);
        }

        // Blocking failures: tabular JSON that could not be parsed (counts toward unreadable).
        if (d.CountedAsUnreadableImportFailure)
        {
            return role == "Border"
                ? new SolidColorBrush(Color.FromArgb(255, 220, 38, 38))
                : new SolidColorBrush(Color.FromArgb(255, 255, 241, 242));
        }

        // Settings / Square parse issues: warning styling only.
        if (d.HasParseError)
        {
            return role == "Border"
                ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
                : new SolidColorBrush(Color.FromArgb(255, 255, 247, 237));
        }

        if (d.IsZeroCountTabularWarning)
        {
            return role == "Border"
                ? new SolidColorBrush(Color.FromArgb(255, 217, 119, 6))
                : new SolidColorBrush(Color.FromArgb(255, 255, 247, 237));
        }

        return Neutral(role);
    }

    private static SolidColorBrush Neutral(string role) =>
        role == "Border"
            ? new SolidColorBrush(Color.FromArgb(255, 226, 232, 240))
            : new SolidColorBrush(Color.FromArgb(255, 248, 250, 252));

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
