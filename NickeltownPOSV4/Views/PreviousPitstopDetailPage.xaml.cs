using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class PreviousPitstopDetailPage : Page
{
    private readonly PreviousPitstopDetailViewModel _viewModel;

    public PreviousPitstopDetailPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<PreviousPitstopDetailViewModel>();
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
