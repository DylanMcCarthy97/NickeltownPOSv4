using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Services.Tabs;

/// <summary>Shared toolbar/panel guard checks for the tabs workspace.</summary>
internal static class TabsWorkspaceCommandGuards
{
    public static bool ToolbarActionsEnabled(
        bool archiveOverlayVisible,
        bool deleteOverlayVisible,
        bool isAddDrinksWorkspaceOpen) =>
        !archiveOverlayVisible && !deleteOverlayVisible && !isAddDrinksWorkspaceOpen;

    public static bool CanOpenDrinksOrFunds(bool toolbarEnabled, TabCardModel? selectedTab) =>
        toolbarEnabled && selectedTab is not null;

    public static bool CanEditTab(bool toolbarEnabled) => toolbarEnabled;

    public static bool CanOpenTabHistory(bool toolbarEnabled) => toolbarEnabled;

    public static bool CanArchiveSelected(
        bool toolbarEnabled,
        bool isAdmin,
        TabCardModel? selectedTab) =>
        toolbarEnabled && isAdmin && selectedTab is not null;

    public static bool CanRestoreSelected(
        bool toolbarEnabled,
        bool isAdmin,
        TabCardModel? selectedTab) =>
        toolbarEnabled && isAdmin && selectedTab is not null;

    public static bool CanDeleteSelected(
        bool toolbarEnabled,
        bool isAdmin,
        TabCardModel? selectedTab) =>
        toolbarEnabled
        && isAdmin
        && selectedTab is not null
        && selectedTab.Balance == 0m;
}
