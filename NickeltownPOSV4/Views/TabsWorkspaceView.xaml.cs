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
        HeaderRowDef.Height = new GridLength(bathurst ? 176 : 56);
        ModeRowDef.Height = new GridLength(bathurst ? 46 : 70);
        ModeSelectorGrid.Height = bathurst ? 46 : 70;
        ModeSelectorGrid.Margin = bathurst ? new Thickness(0) : new Thickness(10, 2, 10, 0);

        foreach (var child in ModeSelectorGrid.Children)
        {
            if (child is Button button)
            {
                button.CornerRadius = bathurst ? new CornerRadius(0) : new CornerRadius(12);
                button.MinHeight = bathurst ? 46 : 50;
            }
        }
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
