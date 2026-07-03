using System;
using Microsoft.UI.Xaml;
using Windows.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Converters;

/// <summary>Highlights the wizard step that matches the current <see cref="MigrationWizardStep"/>.</summary>
public sealed class MigrationWizardStepForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var secondary = ResolveBrush("PosTextSecondaryBrush");
        var accent = ResolveBrush("PosAccentBrush");

        if (parameter is not string p || !Enum.TryParse(p, out MigrationWizardStep target))
        {
            return secondary;
        }

        return value is MigrationWizardStep current && current == target ? accent : secondary;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    private static Brush ResolveBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var resource) == true && resource is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139));
    }
}
