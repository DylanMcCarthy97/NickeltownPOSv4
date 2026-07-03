using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels.Membership;

namespace NickeltownPOSV4.Views.Membership;

public sealed partial class MembershipDashboardPage : Page
{
    private MembershipDashboardViewModel? _viewModel;

    public MembershipDashboardPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<MembershipDashboardViewModel>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (_viewModel is not null)
        {
            await _viewModel.LoadAsync();
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_viewModel is not null)
        {
            await _viewModel.LoadAsync();
        }
    }
}
