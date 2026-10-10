using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Themes;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Converters;

public sealed class BalanceTierToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        TabBalanceTier tier;
        var bathurst = BathurstTabsTheme.IsEnabled;

        if (value is TabsBoardCellViewModel cell)
        {
            tier = cell.DisplayBalanceTier;
            bathurst = cell.IsBathurstTheme;
        }
        else if (value is string key && key.Length > 2)
        {
            // BalanceForegroundKey: "B:Negative" / "N:Low" / …
            bathurst = key[0] == 'B';
            var tierName = key.Length > 2 ? key[2..] : nameof(TabBalanceTier.Good);
            tier = Enum.TryParse<TabBalanceTier>(tierName, ignoreCase: true, out var parsed)
                ? parsed
                : TabBalanceTier.Good;
        }
        else if (value is TabBalanceTier boundTier)
        {
            tier = boundTier;
        }
        else
        {
            return new SolidColorBrush(Microsoft.UI.Colors.SlateGray);
        }

        if (bathurst)
        {
            return BathurstTabsBrushes.FromArgb(BathurstTabsTheme.BalanceColor(tier));
        }

        var resourceKey = TabBalanceTierBrushes.BalanceResourceKey(tier);
        if (Microsoft.UI.Xaml.Application.Current?.Resources.TryGetValue(resourceKey, out var o) == true
            && o is Brush b)
        {
            return b;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.SlateGray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
