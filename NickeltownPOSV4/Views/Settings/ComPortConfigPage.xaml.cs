using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels.Settings;

namespace NickeltownPOSV4.Views.Settings;

public sealed partial class ComPortConfigPage : Page
{
    private ComPortConfigViewModel? _viewModel;

    public ComPortConfigPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<ComPortConfigViewModel>();
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
