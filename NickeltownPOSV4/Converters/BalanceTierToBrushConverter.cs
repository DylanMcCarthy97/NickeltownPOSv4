using System;

using Microsoft.UI.Xaml.Data;

using Microsoft.UI.Xaml.Media;

using NickeltownPOSV4.Models;



namespace NickeltownPOSV4.Converters;



public sealed class BalanceTierToBrushConverter : IValueConverter

{

    public object Convert(object value, Type targetType, object parameter, string language)

    {

        if (value is TabBalanceTier tier)

        {

            var key = TabBalanceTierBrushes.BalanceResourceKey(tier);



            if (Microsoft.UI.Xaml.Application.Current?.Resources.TryGetValue(key, out var o) == true && o is Brush b)

            {

                return b;

            }

        }



        return new SolidColorBrush(Microsoft.UI.Colors.SlateGray);

    }



    public object ConvertBack(object value, Type targetType, object parameter, string language) =>

        throw new NotSupportedException();

}


