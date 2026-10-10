using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class TabsWorkspaceView : UserControl
{
    public TabsWorkspaceView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TabsWorkspaceViewModel>();
        ApplyBathurstChrome();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyBathurstChrome();
        if (DataContext is TabsWorkspaceViewModel vm)
        {
            vm.PropertyChanged -= OnWorkspacePropertyChanged;
            vm.PropertyChanged += OnWorkspacePropertyChanged;
            await vm.RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) =>
        ApplyBathurstChrome();

    private void OnWorkspacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TabsWorkspaceViewModel.IsBathurstTheme) or null or "")
        {
            ApplyBathurstChrome();
        }
    }

    private void ApplyBathurstChrome()
    {
        var bathurst = DataContext is TabsWorkspaceViewModel { IsBathurstTheme: true };
        HeaderRowDef.Height = new GridLength(bathurst ? 168 : 56);
        ModeRowDef.Height = new GridLength(bathurst ? 50 : 70);
        ModeSelectorGrid.Height = bathurst ? 48 : 70;
        RootLayout.Background = bathurst
            ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 11, 13))
            : RootLayout.Background;
    }

    private void BoardCell_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (DataContext is not TabsWorkspaceViewModel vm || sender is not FrameworkElement fe)
        {
            return;
        }

        if (fe.DataContext is not TabsBoardCellViewModel cell)
        {
            return;
        }

        if (cell.IsAddCell)
        {
            vm.OpenAddCardCommand.Execute(null);
            return;
        }

        if (cell.Tab is { } tab)
        {
            vm.SelectTabCommand.Execute(tab);
        }
    }
}
