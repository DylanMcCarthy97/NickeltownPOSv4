using Microsoft.Extensions.DependencyInjection;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;
using NickeltownPOSV4.Views;
// MEMBERSHIP MODULE DISABLED - re-enable in ServiceCollection/App/AdminHome
// using NickeltownPOSV4.Views.Membership;
using NickeltownPOSV4.Views.Panels;

namespace NickeltownPOSV4.DependencyInjection;

internal static class ServiceCollectionNavigationExtensions
{
    public static IServiceCollection AddNavigationServices(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISlidePanelService, SlidePanelService>();
        services.AddSingleton<IInputOverlayService, InputOverlayService>();
        services.AddSingleton<IGuestCloseoutOpenBus, GuestCloseoutOpenBus>();
        services.AddSingleton<IPosThemeService, PosThemeService>();
        services.AddSingleton<WindowHandleProvider>();
        services.AddSingleton<IWindowHandleProvider>(sp => sp.GetRequiredService<WindowHandleProvider>());
        services.AddSingleton<UserSessionService>();
        services.AddSingleton<IUserSessionService>(sp => sp.GetRequiredService<UserSessionService>());
        services.AddSingleton<IAuthSignOutService, AuthSignOutService>();
        services.AddSingleton<ISessionInactivityService, SessionInactivityService>();
        services.AddSingleton<IRootNavigationCoordinator, RootNavigationCoordinator>();
        services.AddSingleton<IStaffPinLookupCache, StaffPinLookupCache>();
        services.AddSingleton<IAuthenticationService, SqliteAuthenticationService>();
        services.AddSingleton<IDefaultStaffBootstrapper, DefaultStaffBootstrapper>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ForcedPinChangeViewModel>();

        services.AddSingleton<MainShellViewModel>();
        services.AddSingleton<WorkspacePageViewModel>();
        services.AddSingleton<TabsWorkspaceViewModel>();

        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<AdminHomePage>();
        // MEMBERSHIP MODULE DISABLED - re-enable in ServiceCollection/App/AdminHome
        // services.AddTransient<MembershipHomePage>();
        services.AddSingleton<ReportsHomeViewModel>();
        services.AddTransient<ReportsHomePage>();
        services.AddTransient<PitstopEndOfDayReportPage>();
        services.AddTransient<PreviousPitstopsPage>();
        services.AddTransient<PreviousPitstopDetailPage>();
        services.AddTransient<PreviousPitstopItemsPage>();
        services.AddTransient<PreviousPitstopTransactionsPage>();
        services.AddTransient<SquareRecoveryViewModel>();
        services.AddTransient<SquareRecoveryPage>();
        services.AddTransient<SystemCheckViewModel>();
        services.AddTransient<SystemCheckPage>();
        services.AddTransient<VoidPitstopSaleViewModel>();
        services.AddTransient<VoidPitstopSalePage>();
        services.AddSingleton<IExportedFileLauncher, WindowsExportedFileLauncher>();
        services.AddSingleton<IReportPathProvider, LocalReportPathProvider>();

        return services;
    }
}
