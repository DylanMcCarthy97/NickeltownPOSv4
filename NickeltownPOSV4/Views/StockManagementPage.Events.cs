using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

/// <summary>XAML event handlers for the stock management page chrome (not in-page modal layer).</summary>
public sealed partial class StockManagementPage
{
    private void StockManagementPage_Loaded(object sender, RoutedEventArgs e)
    {
        DisableHorizontalScroll(ProductBrowserList);
        AttachStockWedgeScan();
        ClearStockSearchFocus();
    }

    private static void DisableHorizontalScroll(FrameworkElement? element)
    {
        if (element is null)
        {
            return;
        }

        ScrollViewer.SetHorizontalScrollMode(element, ScrollMode.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(element, ScrollBarVisibility.Disabled);
    }

    private void StockManagementPage_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachStockWedgeScan();
        _vm.PropertyChanged -= Vm_PropertyChanged;
        App.Services.GetRequiredService<IBarCatalogCache>().Invalidate();
        App.Services.GetRequiredService<PosCatalogAutoRefreshService>().PulseRefresh();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StockManagementPageViewModel.SelectedPageRow))
        {
            TryScrollSelectedIntoView();
        }
    }

    private void TryScrollSelectedIntoView()
    {
        if (ProductBrowserList is null || _vm.SelectedPageRow is null)
        {
            return;
        }

        ProductBrowserList.ScrollIntoView(_vm.SelectedPageRow);
    }

    private async void ProductBrowserList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StockListRowViewModel row && _vm.SelectRowCommand.CanExecute(row))
        {
            _vm.SelectRowCommand.Execute(row);
            _resumeProductSetupAfterItemEdit = false;
            // Stock v2: row tap → action sheet (quick edit / adjust / receive / count / full edit).
            await ShowItemActionSheetAsync(row).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Open the on-screen keyboard only on an intentional tap.
    /// GotFocus must NOT open it — after the overlay closes, focus returning to the
    /// TextBox would reopen the keyboard in a loop (Toshiba stuck-keyboard bug).
    /// </summary>
    private async void StockSearchTextBox_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        await RunWithStockOverlayGateAsync(RunStockSearchOverlayCoreAsync).ConfigureAwait(true);
    }

    private async void StockScanBarcode_Click(object sender, RoutedEventArgs e)
    {
        // Prefer wedge scan on the page; this button is a fallback for typing a code.
        await RunWithStockOverlayGateAsync(async () =>
        {
            var r = await _inputOverlay.ShowKeyboardAsync(string.Empty, "Type barcode").ConfigureAwait(true);
            ClearStockSearchFocus();
            if (r is not null)
            {
                await TryHandleHomeBarcodeOrSearchAsync(r).ConfigureAwait(true);
            }
        }).ConfigureAwait(true);
    }

    private async Task RunStockSearchOverlayCoreAsync()
    {
        var r = await _inputOverlay.ShowKeyboardAsync(_vm.SearchText ?? string.Empty, "Search products").ConfigureAwait(true);
        ClearStockSearchFocus();
        if (r is null)
        {
            return;
        }

        // Exact barcode / unique name → action sheet; otherwise filter the list.
        await TryHandleHomeBarcodeOrSearchAsync(r).ConfigureAwait(true);
    }

    private async void RefreshStock_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.RefreshCommand.CanExecute(null))
        {
            await _vm.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private async void AddItem_Click(object sender, RoutedEventArgs e)
    {
        _resumeProductSetupAfterItemEdit = false;
        await OpenAddItemFullScreenAsync().ConfigureAwait(true);
    }

    private async void ImportStock_Click(object sender, RoutedEventArgs e)
    {
        await OpenImportWizardFullScreenAsync().ConfigureAwait(true);
    }

    private async void EditItemQuick_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasSelection)
        {
            _vm.SetStatusMessage("Select an item from the list first.");
            return;
        }

        _resumeProductSetupAfterItemEdit = false;
        await OpenItemEditFullScreenAsync().ConfigureAwait(true);
    }

    private void ShoppingList_Click(object sender, RoutedEventArgs e) => OpenShoppingListFullScreen();

    private void ReceiveStock_Click(object sender, RoutedEventArgs e) => OpenReceiveStockFullScreen();

    private void CountStock_Click(object sender, RoutedEventArgs e) => OpenCountStockFullScreen();

    private void CatalogHealth_Click(object sender, RoutedEventArgs e) => OpenCatalogHealthFullScreen();
}
