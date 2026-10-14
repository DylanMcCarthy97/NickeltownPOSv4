using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

/// <summary>Stock v2: barcode/find → action sheet; More menu for secondary filters &amp; admin.</summary>
public sealed partial class StockManagementPage
{
    private void StockMore_Click(object sender, RoutedEventArgs e) => ShowStockMoreSheet();

    private void ShowStockMoreSheet()
    {
        var body = new StackPanel { Spacing = 10, MaxWidth = 560 };
        body.Children.Add(new TextBlock
        {
            Text = "Filters and admin",
            FontSize = 13,
            Opacity = 0.75,
        });

        body.Children.Add(BuildSheetActionButton("Drinks only", "Show drink items", async () =>
        {
            DismissInPageStockModal();
            _vm.ApplyQuickFilter(StockManagementBrowserFilter.ChipDrinks);
            await Task.CompletedTask.ConfigureAwait(true);
        }));
        body.Children.Add(BuildSheetActionButton("Merch only", "Show merchandise", async () =>
        {
            DismissInPageStockModal();
            _vm.ApplyQuickFilter(StockManagementBrowserFilter.ChipMerch);
            await Task.CompletedTask.ConfigureAwait(true);
        }));
        body.Children.Add(BuildSheetActionButton("Add item", "Create a new catalogue product", async () =>
        {
            DismissInPageStockModal();
            await OpenAddItemFullScreenAsync().ConfigureAwait(true);
        }));
        body.Children.Add(BuildSheetActionButton("Refresh", "Reload stock from the database", async () =>
        {
            DismissInPageStockModal();
            if (_vm.RefreshCommand.CanExecute(null))
            {
                await _vm.RefreshCommand.ExecuteAsync(null).ConfigureAwait(true);
            }
        }));

        ShowInPageStockModal("More", body, "Close", null, null, dismissOnDimTap: true, modalMaxWidth: 600);
    }

    internal async Task ShowItemActionSheetAsync(StockListRowViewModel row)
    {
        if (_vm.SelectRowCommand.CanExecute(row))
        {
            _vm.SelectRowCommand.Execute(row);
        }

        var body = new StackPanel { Spacing = 10, MaxWidth = 560 };
        body.Children.Add(new TextBlock
        {
            Text = row.CategoryLine + "  ·  " + row.StockHeroText + " on hand",
            FontSize = 14,
            Opacity = 0.8,
        });
        if (!string.IsNullOrWhiteSpace(row.StatusText))
        {
            body.Children.Add(new TextBlock
            {
                Text = row.StatusText,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
        }

        body.Children.Add(BuildSheetActionButton("Quick edit", "Qty, prices, warn, target, active", async () =>
        {
            DismissInPageStockModal();
            await OpenQuickEditAsync(row).ConfigureAwait(true);
        }));

        if (row.CanQuickAdjustStock)
        {
            body.Children.Add(BuildSheetActionButton("Adjust stock", "Add or remove with + / −", async () =>
            {
                DismissInPageStockModal();
                ShowQuickStockAdjustFlyout(row);
                await Task.CompletedTask.ConfigureAwait(true);
            }));
        }

        body.Children.Add(BuildSheetActionButton("Receive", "Add stock from a purchase", async () =>
        {
            DismissInPageStockModal();
            OpenReceiveStockFullScreenForItem(row.ItemId);
            await Task.CompletedTask.ConfigureAwait(true);
        }));

        body.Children.Add(BuildSheetActionButton("Count this item", "Set the actual counted qty", async () =>
        {
            DismissInPageStockModal();
            OpenCountStockFullScreenForItem(row.ItemId);
            await Task.CompletedTask.ConfigureAwait(true);
        }));

        body.Children.Add(BuildSheetActionButton("Full edit…", "Name, barcodes, specials, advanced", async () =>
        {
            DismissInPageStockModal();
            _resumeProductSetupAfterItemEdit = false;
            await OpenItemEditFullScreenAsync().ConfigureAwait(true);
        }));

        ShowInPageStockModal(row.Name, body, "Cancel", null, null, dismissOnDimTap: true, modalMaxWidth: 620);
    }

    internal async Task TryHandleHomeBarcodeOrSearchAsync(string raw)
    {
        var code = (raw ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return;
        }

        var matches = _vm.FindItemsBySearch(code, maxResults: 8);
        StockEditorRow? exact = null;
        foreach (var m in matches)
        {
            if (StockAlternateSkuHelper.MatchesPrimaryOrAlternate(m.Sku, m.AlternateSkusJson, code)
                || string.Equals((m.Name ?? string.Empty).Trim(), code, StringComparison.OrdinalIgnoreCase))
            {
                exact = m;
                break;
            }
        }

        if (exact is null && matches.Count == 1)
        {
            exact = matches[0];
        }

        if (exact is null)
        {
            _vm.SearchText = code;
            _vm.SetStatusMessage(matches.Count == 0
                ? $"No item matches \"{code}\"."
                : $"{matches.Count} matches — tap one in the list.");
            return;
        }

        var row = _vm.FindListRowByItemId(exact.ItemId)
            ?? new StockListRowViewModel(exact, isAlternateRow: false);
        await ShowItemActionSheetAsync(row).ConfigureAwait(true);
    }

    private Button BuildSheetActionButton(string title, string subtitle, Func<Task> onClick)
    {
        var btn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = 64,
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(12),
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        btn.Content = stack;
        btn.Click += async (_, _) => await onClick().ConfigureAwait(true);
        return btn;
    }

    /// <summary>Receive workflow pre-selecting an item when opened from the action sheet.</summary>
    internal void OpenReceiveStockFullScreenForItem(long itemId)
    {
        _pendingReceiveItemId = itemId;
        OpenReceiveStockFullScreen();
    }

    /// <summary>Count workflow starting on a specific item when opened from the action sheet.</summary>
    internal void OpenCountStockFullScreenForItem(long itemId)
    {
        _pendingCountItemId = itemId;
        OpenCountStockFullScreen();
    }
}
