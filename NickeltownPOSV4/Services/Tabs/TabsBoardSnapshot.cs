using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Services.Tabs;

/// <summary>Cheap compare so the tabs board is not rebuilt when nothing changed.</summary>
internal static class TabsBoardSnapshot
{
    public static string Fingerprint(IReadOnlyList<TabCardModel> tabs)
    {
        var text = new StringBuilder(tabs.Count * 32);
        foreach (var tab in tabs)
        {
            text.Append(tab.Id)
                .Append('|')
                .Append(tab.Balance.ToString("0.00", CultureInfo.InvariantCulture))
                .Append('|')
                .Append(tab.OpenDrinkCount)
                .Append('|')
                .Append(tab.LastActivityAt)
                .Append('|')
                .Append(tab.LastDrinkLine)
                .Append('|')
                .Append(tab.IsGuest ? 'G' : 'M')
                .Append(';');
        }

        return text.ToString();
    }
}
