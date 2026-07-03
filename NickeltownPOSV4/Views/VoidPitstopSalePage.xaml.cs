using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class VoidPitstopSalePage : Page
{
    public VoidPitstopSalePage(VoidPitstopSaleViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public VoidPitstopSaleViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }
}
