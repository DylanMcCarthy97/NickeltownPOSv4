using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

/// <summary>
/// Settings page now hosts only the General tiles (Switch mode / Logout / Exit).
/// Admin tools moved to <see cref="AdminHomePage"/> which is reached via the bottom-nav Admin tab.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private SettingsPageViewModel? _viewModel;

    public SettingsPage()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<SettingsPageViewModel>();
        DataContext = _viewModel;
        _viewModel.AttachXamlRoot(() => XamlRoot);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _viewModel?.AttachXamlRoot(() => XamlRoot);
        _viewModel?.RefreshFromSession();
    }
}
