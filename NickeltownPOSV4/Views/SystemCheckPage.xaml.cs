using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class SystemCheckPage : Page
{
    private readonly SystemCheckViewModel _viewModel;

    public SystemCheckPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<SystemCheckViewModel>();
        DataContext = _viewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await _viewModel.InitializeAsync();
    }
}
