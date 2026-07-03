using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels.Membership;

namespace NickeltownPOSV4.Views.Membership;

public sealed partial class MembershipApplicationsPage : Page
{
    private MembershipApplicationsViewModel? _viewModel;

    public MembershipApplicationsPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<MembershipApplicationsViewModel>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
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
        if (_viewModel is null)
        {
            return;
        }

        var successMessage = e.Parameter as string;
        await _viewModel.LoadAsync(successMessage);
    }
}
