using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class SquareRecoveryPage : Page
{
    private readonly SquareRecoveryViewModel _viewModel;

    public SquareRecoveryPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<SquareRecoveryViewModel>();
        DataContext = _viewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await _viewModel.InitializeAsync();
    }
}
