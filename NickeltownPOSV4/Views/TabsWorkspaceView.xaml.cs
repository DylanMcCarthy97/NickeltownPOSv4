using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Services.Tabs;
using NickeltownPOSV4.Themes;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class TabsWorkspaceView : UserControl
{
    private readonly ITabsVisualThemeService _tabsVisualTheme;
    private TabsWorkspaceViewModel? _subscribedVm;

    public TabsWorkspaceView()
    {
        InitializeComponent();
        _tabsVisualTheme = App.Services.GetRequiredService<ITabsVisualThemeService>();
        DataContext = App.Services.GetRequiredService<TabsWorkspaceViewModel>();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }

        if (args.NewValue is TabsWorkspaceViewModel vm)
        {
            _subscribedVm = vm;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TabsWorkspaceViewModel.IsBathurstTheme)
            or nameof(TabsWorkspaceViewModel.IsBathurstVisualActive)
            or null
            or "")
        {
            ApplyBathurstChrome();
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _tabsVisualTheme.Changed += OnTabsThemeChanged;
        ApplyBathurstChrome();
        if (DataContext is TabsWorkspaceViewModel vm)
        {
            await vm.RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _tabsVisualTheme.Changed -= OnTabsThemeChanged;
        if (_subscribedVm is not null)
        {
            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribedVm = null;
        }
    }

    private void OnTabsThemeChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(ApplyBathurstChrome);

    private void ApplyBathurstChrome()
    {
        var bathurst = DataContext is TabsWorkspaceViewModel { IsBathurstTheme: true };

        HeaderRow.Height = new GridLength(bathurst ? 88 : 56);
        ModeRow.Height = new GridLength(bathurst ? 52 : 70);
        ModeSelector.Height = bathurst ? 52 : 70;
        ModeSelector.Margin = bathurst ? new Thickness(8, 0, 8, 0) : new Thickness(10, 2, 10, 0);

        MainContent.Margin = bathurst ? new Thickness(8) : new Thickness(10, 4, 10, 4);
        if (bathurst)
        {
            LeftColumn.Width = new GridLength(688);
            GapColumn.Width = new GridLength(8);
            RightColumn.Width = new GridLength(312);
            BoardFooterRow.Height = new GridLength(44);
            BoardFooter.Height = 44;
            FreeItemsLayout.MinItemHeight = 64;
            FreeItemsLayout.MinColumnSpacing = 8;
            FreeItemsLayout.MinRowSpacing = 8;
            RightPanel.Padding = new Thickness(8);
            RightPanel.CornerRadius = new CornerRadius(6);
            MemberActionsGrid.ColumnSpacing = 8;
            MemberActionsGrid.RowSpacing = 8;
        }
        else
        {
            LeftColumn.Width = new GridLength(7, GridUnitType.Star);
            GapColumn.Width = new GridLength(12);
            RightColumn.Width = new GridLength(3, GridUnitType.Star);
            BoardFooterRow.Height = new GridLength(40);
            BoardFooter.Height = 40;
            FreeItemsLayout.MinItemHeight = 78;
            FreeItemsLayout.MinColumnSpacing = 6;
            FreeItemsLayout.MinRowSpacing = 6;
            RightPanel.Padding = new Thickness(10, 8, 10, 8);
            RightPanel.CornerRadius = new CornerRadius(12);
            MemberActionsGrid.ColumnSpacing = 6;
            MemberActionsGrid.RowSpacing = 6;
        }

        NormalHeader.Visibility = bathurst ? Visibility.Collapsed : Visibility.Visible;
        BathurstHeader.Visibility = bathurst ? Visibility.Visible : Visibility.Collapsed;

        ApplyScopedBrushes(bathurst);
        UpdateStyleDouble(Resources, "TabsModeSegmentStyle", FrameworkElement.MinHeightProperty, bathurst ? 44 : 50);
        UpdateStyleDouble(Resources, "TabsSideActionButtonStyle", FrameworkElement.MinHeightProperty, bathurst ? 48 : 64);
        UpdateStyleDouble(Resources, "QuickFreeItemTileStyle", FrameworkElement.MinHeightProperty, bathurst ? 64 : 78);

        if (Resources["QuickFreeItemTileStyle"] is Style freeStyle)
        {
            ReplaceSetter(
                freeStyle,
                Control.BackgroundProperty,
                bathurst
                    ? Resources["BathurstFreeItemBrush"]
                    : Resources["PosButtonPrimaryBrush"]);
        }

        if (Resources["TabsModeSegmentStyle"] is Style modeStyle)
        {
            ReplaceSetter(modeStyle, Control.CornerRadiusProperty, new CornerRadius(bathurst ? 6 : 12));
            ReplaceSetter(modeStyle, Control.PaddingProperty, bathurst ? new Thickness(8, 4) : new Thickness(10, 8));
        }
    }

    private void ApplyScopedBrushes(bool bathurst)
    {
        foreach (var key in BathurstTabsPalette.AllPosKeyColors.Keys)
        {
            if (!Resources.TryGetValue(key, out var localObj) || localObj is not SolidColorBrush local)
            {
                continue;
            }

            if (bathurst && BathurstTabsPalette.TryGetColor(key, out var bathurstColor))
            {
                local.Color = bathurstColor;
            }
            else if (Application.Current.Resources.TryGetValue(key, out var appObj) && appObj is SolidColorBrush appBrush)
            {
                local.Color = appBrush.Color;
            }
        }

        if (Resources.TryGetValue("PosCanvasBrush", out var canvas) && canvas is Brush canvasBrush)
        {
            RootLayout.Background = canvasBrush;
        }
    }

    private static void UpdateStyleDouble(ResourceDictionary resources, string styleKey, DependencyProperty property, double value)
    {
        if (resources[styleKey] is Style style)
        {
            ReplaceSetter(style, property, value);
        }
    }

    private static void ReplaceSetter(Style style, DependencyProperty property, object value)
    {
        for (var i = style.Setters.Count - 1; i >= 0; i--)
        {
            if (style.Setters[i] is Setter existing && ReferenceEquals(existing.Property, property))
            {
                style.Setters.RemoveAt(i);
            }
        }

        style.Setters.Add(new Setter(property, value));
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
