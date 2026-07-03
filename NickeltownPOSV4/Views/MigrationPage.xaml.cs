using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class MigrationPage : Page
{
    private ShellRoute? _returnRoute;

    public MigrationPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MigrationWizardViewModel>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _returnRoute = e.Parameter as ShellRoute;
    }

    private void Back_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var nav = App.Services.GetRequiredService<INavigationService>();
        var route = _returnRoute ?? App.Services.GetRequiredService<MainShellViewModel>().AdminRoute;
        if (string.Equals(route.Id, "settings", System.StringComparison.OrdinalIgnoreCase))
        {
            nav.Navigate(typeof(SettingsPage));
            return;
        }

        nav.Navigate(typeof(WorkspacePage), route);
    }
}
