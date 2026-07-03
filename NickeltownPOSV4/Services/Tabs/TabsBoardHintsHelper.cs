using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsBoardHintsHelper
{
    public const string SqliteErrorMessage = "Could not read tabs from SQLite. Check the database and try again.";

    public static string BuildLiveTabsStatusHint(IReadOnlyList<TabCardModel> openTabs)
    {
        var n = openTabs.Count;
        var g = openTabs.Count(t => t.IsGuest);
        var prefix = g > 0
            ? $"Live data: {n} open tab(s) from SQLite (guest tabs open under Guests)."
            : $"Live data: {n} open tab(s) from SQLite.";
        if (n == 0)
        {
            return prefix;
        }

        var names = openTabs.Take(3).Select(t => t.DisplayName).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (names.Count == 0)
        {
            return prefix;
        }

        var sample = string.Join(" · ", names);
        const int maxLen = 72;
        if (sample.Length > maxLen)
        {
            sample = sample[..maxLen] + "…";
        }

        return $"{prefix} e.g. {sample}";
    }

    public static string FormatToolbarHintLine(string? operatorHint, TabCardModel? selectedTab)
    {
        if (!string.IsNullOrWhiteSpace(operatorHint))
        {
            return operatorHint.Trim();
        }

        if (selectedTab is not null)
        {
            var name = (selectedTab.DisplayName ?? string.Empty).Trim();
            return string.IsNullOrEmpty(name) ? "Selected tab" : $"Selected tab: {name}";
        }

        return string.Empty;
    }

    public static string GuestSelectedOperatorHint(TabCardModel tab) =>
        $"Guest selected: {tab.DisplayName} — use Drinks or Funds on the board.";

    public const string BarTabsPageTitle = "Bar Tabs";

    public static string FormatWelcomeText(bool isSignedIn, string? displayName) =>
        isSignedIn && !string.IsNullOrWhiteSpace(displayName)
            ? $"Welcome, {displayName.Trim()}"
            : "Welcome — sign in to continue";
}
