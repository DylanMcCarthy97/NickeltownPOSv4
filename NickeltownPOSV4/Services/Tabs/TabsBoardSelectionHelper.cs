using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsBoardSelectionHelper
{
    public static TabCardModel? FindById(IEnumerable<TabCardModel> openTabs, string tabId) =>
        openTabs.FirstOrDefault(t => t.Id == tabId);

    public static void ClearSelection(IEnumerable<TabCardModel> openTabs)
    {
        foreach (var t in openTabs)
        {
            t.IsSelected = false;
        }
    }
}
