using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Updates;

namespace NickeltownPOSV4.Views;

public sealed partial class StartupPage : Page
{
    private bool _bootstrapStarted;

    public StartupPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Disabled;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_bootstrapStarted)
        {
            return;
        }

        _bootstrapStarted = true;
        _ = RunBootstrapAsync();
    }

    private async System.Threading.Tasks.Task RunBootstrapAsync()
    {
        if (XamlRoot is not null)
        {
            StatusText.Text = "Checking for data to migrate…";
            await AppStorageMigrationUiHelper.RunStartupMigrationsAsync(XamlRoot).ConfigureAwait(true);
        }

        var bootstrap = App.Services.GetRequiredService<IAppBootstrapService>();
        var rootNav = App.Services.GetRequiredService<IRootNavigationCoordinator>();
        var progress = new Progress<string>(msg =>
        {
            TcxLayoutDiagnostics.TryEnqueueNormal(() => StatusText.Text = msg);
        });

        var result = await bootstrap.RunAsync(progress).ConfigureAwait(true);
        if (result.Ok)
        {
            if (XamlRoot is not null)
            {
                await AppUpdateRestartHelper.ShowUpdatedNotificationIfNeededAsync(XamlRoot).ConfigureAwait(true);

                var updating = await AppUpdateUiHelper.TryHandleStartupUpdateAsync(XamlRoot).ConfigureAwait(true);
                if (updating)
                {
                    return;
                }
            }

            rootNav.NavigateToLogin();
            return;
        }

        StatusText.Text = "Startup failed";
        ErrorText.Text = result.ErrorMessage ?? "Could not start the application.";
        ErrorText.Visibility = Visibility.Visible;
        CloseAppButton.Visibility = Visibility.Visible;
    }

    private void CloseApp_Click(object sender, RoutedEventArgs e) => Application.Current.Exit();
}
