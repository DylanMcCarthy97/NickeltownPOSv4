using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NickeltownPOSV4.Services.Logging;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class TabsWorkspaceView : UserControl
{
    public TabsWorkspaceView()
    {
        LoginTransitionProbe.Mark("tabsView.ctor.begin");
        InitializeComponent();
        LoginTransitionProbe.Mark("tabsView.ctor.xaml");
        DataContext = App.Services.GetRequiredService<TabsWorkspaceViewModel>();
        ApplyBathurstChrome();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        LoginTransitionProbe.Mark("tabsView.ctor.done");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyBathurstChrome();
        if (DataContext is TabsWorkspaceViewModel vm)
        {
            vm.PropertyChanged -= OnWorkspacePropertyChanged;
            vm.PropertyChanged += OnWorkspacePropertyChanged;
            LoginTransitionProbe.Mark("tabsView.refresh.begin");
            await vm.RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
            LoginTransitionProbe.Complete("tabsView.refresh.done");
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) =>
        ApplyBathurstChrome();

    private void OnWorkspacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TabsWorkspaceViewModel.IsAddDrinksWorkspaceOpen)
            or nameof(TabsWorkspaceViewModel.CurrentAddDrinksViewModel))
        {
            EnsureAddDrinksHost(sender as TabsWorkspaceViewModel);
        }

        if (e.PropertyName is nameof(TabsWorkspaceViewModel.IsBathurstTheme)
            or nameof(TabsWorkspaceViewModel.CanToggleBathurstTheme)
            or null
            or "")
        {
            ApplyBathurstChrome();
        }
    }

    private bool _addDrinksHostRealized;

    private void EnsureAddDrinksHost(TabsWorkspaceViewModel? vm)
    {
        if (vm is not { IsAddDrinksWorkspaceOpen: true } || _addDrinksHostRealized)
        {
            return;
        }

        LoginTransitionProbe.Mark("addDrinks.realize.begin");
        _ = FindName(nameof(AddDrinksHost));
        _addDrinksHostRealized = true;
        LoginTransitionProbe.Mark("addDrinks.realize.done");
    }

    private void ApplyBathurstChrome()
    {
        var vm = DataContext as TabsWorkspaceViewModel;
        var bathurst = vm is { IsBathurstTheme: true };
        var roomyFooter = bathurst || vm is { CanToggleBathurstTheme: true };
        HeaderRowDef.Height = new GridLength(bathurst ? 112 : 56);
        ModeRowDef.Height = new GridLength(bathurst ? 52 : 70);
        ModeSelectorGrid.Height = bathurst ? 52 : 70;
        ModeSelectorGrid.Margin = bathurst ? new Thickness(8, 0, 8, 0) : new Thickness(10, 2, 10, 0);
        // Bathurst pager nav targets are 44×44 inside a taller chrome pill.
        BoardFooterRowDef.Height = new GridLength(roomyFooter ? 56 : 40);
        BoardFooterGrid.Height = roomyFooter ? 56 : 40;

        foreach (var child in ModeSelectorGrid.Children)
        {
            if (child is Button button)
            {
                button.CornerRadius = bathurst ? new CornerRadius(22) : new CornerRadius(12);
                button.MinHeight = bathurst ? 44 : 50;
            }
        }

        if (UndoLastButton is not null)
        {
            UndoLastButton.MinHeight = roomyFooter ? 44 : 36;
        }

        BoardPager.UseDarkChrome = bathurst;
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
