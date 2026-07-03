using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.TabFavorites;
using NickeltownPOSV4.Services.Tabs;
using NickeltownPOSV4.ViewModels;
using NickeltownPOSV4.Views.Panels;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionTabExtensions
{
    public static IServiceCollection AddTabServices(this IServiceCollection services)
    {
        services.AddAddDrinksServices();
        services.AddSingleton<ITabPanelTargetState, TabPanelTargetState>();
        services.AddSingleton<ITabPanelTargetBinder, TabPanelTargetBinder>();
        services.AddSingleton<ITabsManagementUndoService, TabsManagementUndoService>();
        services.AddSingleton<AddDrinksWorkspaceNavigator>();
        services.AddSingleton<IAddDrinksWorkspaceNavigator>(sp => sp.GetRequiredService<AddDrinksWorkspaceNavigator>());
        services.AddSingleton<IAddDrinksSession, AddDrinksSession>();
        services.AddSingleton<IAddFundsSession, AddFundsSession>();
        services.AddSingleton<IEditTabSession, EditTabSession>();
        services.AddSingleton<ITabHistorySession, TabHistorySession>();
        services.AddSingleton<ITabFundsService, SqliteTabFundsService>();
        services.AddSingleton<ITabBarFavoritesHistoryQuery, SqliteTabBarFavoritesHistoryQuery>();
        services.AddSingleton<ITabBarFavoritesService, SqliteTabBarFavoritesService>();
        services.AddSingleton<ITabWorkspaceRefreshBus, TabWorkspaceRefreshBus>();
        services.AddSingleton<ITabEntryService, SqliteTabDrinkSalesService>();
        services.AddSingleton<ITabHistoryQuery, SqliteTabHistoryQuery>();
        services.AddSingleton<ITabWorkspaceUndoStack, TabWorkspaceUndoStack>();
        services.AddSingleton<ITabManagementRepository, SqliteTabManagementRepository>();
        services.AddSingleton<IEditTabPanelHost, EditTabPanelHost>();

        services.AddTransient<AddDrinksPanelViewModel>();
        services.AddTransient<AddDrinksPanel>();
        services.AddTransient<AddFundsPanelViewModel>();
        services.AddTransient<AddFundsPanel>();
        services.AddTransient<TabHistoryPanelViewModel>();
        services.AddTransient<TabHistoryPanel>();
        services.AddTransient<NewTabPanelViewModel>();
        services.AddTransient<NewTabPanel>();
        services.AddTransient<GuestTabPanelViewModel>();
        services.AddTransient<GuestTabPanel>();
        services.AddTransient<GuestCloseoutPanelViewModel>();
        services.AddTransient<GuestCloseoutPanel>();
        services.AddTransient<GuestTabCloseoutPanelViewModel>();
        services.AddTransient<GuestTabCloseoutPanel>();
        services.AddTransient<EditTabPanelViewModel>();
        services.AddTransient<EditTabPanel>();
        services.AddTransient<ArchivedTabsPanelViewModel>();
        services.AddTransient<ArchivedTabsPanel>();
        services.AddTransient<BarModeHelpPanelViewModel>();
        services.AddTransient<BarModeHelpPanel>();
        services.AddTransient<TabsMoreActionsPanel>();

        return services;
    }
}
