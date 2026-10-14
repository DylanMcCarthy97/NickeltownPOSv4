using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.ViewModels.Settings;

namespace NickeltownPOSV4.Views.Settings;

public sealed partial class EmailConfigPage : Page
{
    private readonly EmailConfigViewModel _viewModel;

    public EmailConfigPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<EmailConfigViewModel>();
        DataContext = _viewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Single load path — do not also load on Loaded (that left edit commands stuck disabled).
        await _viewModel.LoadAsync();
    }
}
