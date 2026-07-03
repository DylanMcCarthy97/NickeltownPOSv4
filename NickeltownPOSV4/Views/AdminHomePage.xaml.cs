using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

/// <summary>
/// Admin home, opened from the bottom-nav "Admin" button (Admin role).
/// Reuses <see cref="SettingsPageViewModel"/> for the tile commands so we don't duplicate
/// backup / monthly export / user management plumbing.
/// </summary>
public sealed partial class AdminHomePage : Page
{
    private SettingsPageViewModel? _viewModel;

    public AdminHomePage()
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
        if (_viewModel is not null)
        {
            _ = _viewModel.RefreshPaymentRecoveryAlertAsync();
        }
    }
}
