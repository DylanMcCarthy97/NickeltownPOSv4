using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace NickeltownPOSV4.Converters;

/// <summary>Touch chip affordance: thicker border when selected.</summary>
public sealed class BoolToThicknessConverter : IValueConverter
{
    public double SelectedThickness { get; set; } = 2;

    public double UnselectedThickness { get; set; } = 1;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var selected = value is true;
        var t = selected ? SelectedThickness : UnselectedThickness;
        return new Thickness(t);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
