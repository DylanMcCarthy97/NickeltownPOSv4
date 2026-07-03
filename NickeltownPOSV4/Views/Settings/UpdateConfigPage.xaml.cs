using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Services.Updates;
using NickeltownPOSV4.ViewModels.Settings;

namespace NickeltownPOSV4.Views.Settings;

public sealed partial class UpdateConfigPage : Page
{
    private UpdateConfigViewModel? _viewModel;

    public UpdateConfigPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<UpdateConfigViewModel>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (_viewModel is not null)
        {
            _viewModel.AttachXamlRoot(() => XamlRoot);
            await _viewModel.LoadAsync();
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_viewModel is not null)
        {
            _viewModel.AttachXamlRoot(() => XamlRoot);
            await _viewModel.LoadAsync();
        }
    }
}
