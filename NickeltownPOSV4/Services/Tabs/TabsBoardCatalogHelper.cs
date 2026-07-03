using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Services.Tabs;

/// <summary>Open-tab ordering and member-board filter for the tabs workspace.</summary>
internal static class TabsBoardCatalogHelper
{
    public static IReadOnlyList<TabCardModel> SortForBoard(IEnumerable<TabCardModel> tabs) =>
        tabs.OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

    public static IEnumerable<TabCardModel> MemberTabsOnly(IEnumerable<TabCardModel> allOpenTabs) =>
        allOpenTabs.Where(t => !t.IsGuest);
}
