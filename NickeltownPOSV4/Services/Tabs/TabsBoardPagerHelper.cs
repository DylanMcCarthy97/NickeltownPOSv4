using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsBoardPagerHelper
{
    public const int BoardPageCapacity = 9;

    /// <summary>Total board pages. When <paramref name="includeAddCard"/> is true, the add card may occupy the last slot on the final tab page or a dedicated page when the tab count is a multiple of <see cref="BoardPageCapacity"/>.</summary>
    public static int TotalPages(int tabCount, bool includeAddCard)
    {
        if (!includeAddCard)
        {
            return Math.Max(1, (int)Math.Ceiling(tabCount / (double)BoardPageCapacity));
        }

        if (tabCount == 0)
        {
            return 1;
        }

        var remainder = tabCount % BoardPageCapacity;
        var fullTabPages = tabCount / BoardPageCapacity;
        return remainder == 0 ? fullTabPages + 1 : Math.Max(1, fullTabPages + 1);
    }

    public static int ClampPage(int page, int tabCount, bool includeAddCard)
    {
        var total = TotalPages(tabCount, includeAddCard);
        if (page > total - 1)
        {
            return Math.Max(0, total - 1);
        }

        return page < 0 ? 0 : page;
    }

    public static bool IsAddCardOnlyPage(int tabCount, int currentPage, bool includeAddCard) =>
        includeAddCard && tabCount > 0 && tabCount % BoardPageCapacity == 0 && currentPage == tabCount / BoardPageCapacity;

    public static void ApplyPageToBoardSlots(
        IList<TabsBoardCellViewModel> slots,
        IReadOnlyList<TabCardModel> boardTabs,
        int currentPage,
        bool includeAddCard,
        TabsBoardCellKind addCardKind)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            slots[i].Clear();
        }

        var tabCount = boardTabs.Count;

        if (IsAddCardOnlyPage(tabCount, currentPage, includeAddCard))
        {
            slots[0].SetAddCard(addCardKind);
            return;
        }

        var tabStart = currentPage * BoardPageCapacity;
        var tabsThisPage = Math.Min(BoardPageCapacity, Math.Max(0, tabCount - tabStart));

        for (var i = 0; i < tabsThisPage; i++)
        {
            slots[i].AttachTab(boardTabs[tabStart + i]);
        }

        var isFinalTabPage = tabStart + tabsThisPage >= tabCount;
        if (includeAddCard && isFinalTabPage && !IsAddCardOnlyPage(tabCount, currentPage, includeAddCard))
        {
            slots[tabsThisPage].SetAddCard(addCardKind);
        }
        else if (includeAddCard && tabCount == 0)
        {
            slots[0].SetAddCard(addCardKind);
        }
    }

    public static string FormatPageInfo(int currentPage, int totalPages) =>
        $"Page {currentPage + 1} of {totalPages}";
}
