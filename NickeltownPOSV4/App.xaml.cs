using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using NickeltownPOSV4.DependencyInjection;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Logging;
using NickeltownPOSV4.Themes;
using System;

namespace NickeltownPOSV4;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();
    }

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        TcxLayoutDiagnostics.SetUiDispatcher(DispatcherQueue.GetForCurrentThread());

        Services.GetRequiredService<IPosThemeService>().Apply(UiThemeId.Light);
        _window = new MainWindow();
        Services.GetRequiredService<ScheduledMaintenanceService>().Start();
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        if (dispatcher is not null)
        {
            Services.GetRequiredService<PosCatalogAutoRefreshService>().Start(dispatcher);
        }

        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(PosLog.CurrentLogFilePath));
        });

        services
            .AddSingleton<IAppBootstrapService, AppBootstrapService>()
            .AddNavigationServices()
            .AddDataServices()
            .AddTabServices()
            .AddPitstopServices()
            .AddStockServices()
            .AddSettingsServices()
            .AddPaymentServices()
            .AddMigrationServices();
            // MEMBERSHIP MODULE DISABLED - re-enable in ServiceCollection/App/AdminHome
            // .AddMembershipServices();

        return services.BuildServiceProvider();
    }
}
