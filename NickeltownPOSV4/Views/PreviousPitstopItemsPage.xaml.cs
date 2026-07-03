using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class PreviousPitstopItemsPage : Page
{
    private readonly PreviousPitstopItemsViewModel _viewModel;

    public PreviousPitstopItemsPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<PreviousPitstopItemsViewModel>();
        DataContext = _viewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is long batchId)
        {
            await _viewModel.LoadAsync(batchId);
        }
    }
}
