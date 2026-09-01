using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels.Settings;

namespace NickeltownPOSV4.Views.Settings;

public sealed partial class QuickFreeItemsConfigPage : Page
{
    private QuickFreeItemsConfigViewModel? _viewModel;

    public QuickFreeItemsConfigPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<QuickFreeItemsConfigViewModel>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (_viewModel is not null)
        {
            await _viewModel.RefreshAsync();
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_viewModel is not null)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private void OnCandidateClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is QuickFreeProductCandidate candidate)
        {
            _viewModel?.SelectCandidate(candidate);
        }
    }
}
