using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class PreviousPitstopTransactionsPage : Page
{
    private readonly PreviousPitstopTransactionsViewModel _viewModel;

    public PreviousPitstopTransactionsPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<PreviousPitstopTransactionsViewModel>();
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
