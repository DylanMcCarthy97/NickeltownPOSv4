using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.AddDrinks;
using NickeltownPOSV4.Services.Migration;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

/// <summary>In-page modal layer (StockModalLayer) and wizard dialogs built in code-behind.</summary>
public sealed partial class StockManagementPage
{
    // Modal chrome event handlers are wired from StockManagementPage.xaml (names must stay stable).
    private void ShowInPageStockModal(
        string title,
        UIElement body,
        string closeButtonText,
        string? primaryButtonText,
        Func<Task>? onPrimaryAsync,
        bool dismissOnDimTap = true,
        double? modalMaxWidth = null,
        double? modalMaxHeight = null)
    {
        _stockModalDimDismissEnabled = dismissOnDimTap;
        _stockModalPrimaryAsync = onPrimaryAsync;
        StockModalTitleText.Text = title;
        StockModalBodyPresenter.Content = body;
        StockModalCloseButton.Content = closeButtonText;

        if (modalMaxWidth is { } w)
        {
            StockModalCard.MaxWidth = w;
        }
        else
        {
            StockModalCard.MaxWidth = 920;
        }

        if (modalMaxHeight is { } h)
        {
            StockModalCard.MaxHeight = h;
        }
        else
        {
            StockModalCard.MaxHeight = 760;
        }

        if (string.IsNullOrEmpty(primaryButtonText) || onPrimaryAsync is null)
        {
            StockModalPrimaryButton.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(StockModalCloseButton, 2);
        }
        else
        {
            StockModalPrimaryButton.Visibility = Visibility.Visible;
            StockModalPrimaryButton.Content = primaryButtonText;
            Grid.SetColumnSpan(StockModalCloseButton, 1);
        }

        StockModalLayer.Visibility = Visibility.Visible;
    }

    private void DismissInPageStockModal()
    {
        StockModalLayer.Visibility = Visibility.Collapsed;
        StockModalBodyPresenter.Content = null;
        _stockModalPrimaryAsync = null;
    }

    private async void StockModalPrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        var fn = _stockModalPrimaryAsync;
        if (fn is null)
        {
            return;
        }

        StockModalPrimaryButton.IsEnabled = false;
        try
        {
            await fn().ConfigureAwait(true);
        }
        finally
        {
            StockModalPrimaryButton.IsEnabled = true;
        }
    }

    private void StockModalCloseButton_Click(object sender, RoutedEventArgs e)
    {
        DismissInPageStockModal();
    }

    private void StockModalDim_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_stockModalDimDismissEnabled)
        {
            return;
        }

        DismissInPageStockModal();
    }

    private static ComboBox CreateStockModePicker(string currentMode)
    {
        var cb = new ComboBox { MinHeight = 46, FontSize = 13 };
        foreach (var (value, label) in new (string Value, string Label)[]
        {
            (StockCatalogTaxonomy.StockModeTracked, "Tracked"),
            (StockCatalogTaxonomy.StockModeSellUntilGone, "Sell until gone"),
            (StockCatalogTaxonomy.StockModeOrderIn, "Order Only"),
            (StockCatalogTaxonomy.StockModeNotTracked, "Not counted"),
        })
        {
            cb.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        }

        var normalized = StockCatalogTaxonomy.NormalizeStockMode(currentMode);
        foreach (var obj in cb.Items)
        {
            if (obj is ComboBoxItem { Tag: string tag } && tag == normalized)
            {
                cb.SelectedItem = obj;
                break;
            }
        }
        return cb;
    }

    private static (ComboBox BucketBox, ComboBox SubBox) CreateCatalogBucketSubPickers(string initialBucket, string initialSub)
    {
        var bucketBox = new ComboBox { MinHeight = 46, FontSize = 13 };
        foreach (var b in new[]
        {
            StockCatalogTaxonomy.BucketBar,
            StockCatalogTaxonomy.BucketPitstop,
            StockCatalogTaxonomy.BucketShared,
        })
        {
            bucketBox.Items.Add(b);
        }

        bucketBox.SelectedItem = StockCatalogTaxonomy.NormalizeBucket(initialBucket);
        var subBox = new ComboBox { MinHeight = 46, FontSize = 13 };

        void ResyncSubs(string preferredSub)
        {
            var b = StockCatalogTaxonomy.NormalizeBucket(bucketBox.SelectedItem as string);
            var allowed = StockCatalogTaxonomy.SubCategoriesForBucket(b).ToList();
            subBox.Items.Clear();
            foreach (var s in allowed)
            {
                subBox.Items.Add(s);
            }

            var pick = StockCatalogTaxonomy.NormalizeSubCategory(b, preferredSub);
            subBox.SelectedItem = allowed.Contains(pick) ? pick : StockCatalogTaxonomy.DefaultSubCategory(b);
        }

        ResyncSubs(initialSub);
        bucketBox.SelectionChanged += (_, _) => ResyncSubs(subBox.SelectedItem as string ?? string.Empty);

        return (bucketBox, subBox);
    }

    private static void ApplyCatalogBucketToVmVisibility(string bucket, StockManagementPageViewModel vm)
    {
        var (showBar, showPit) = StockCatalogTaxonomy.ExpectedVisibilityForBucket(bucket);
        vm.ShowInBar = showBar != 0;
        vm.ShowInPitstop = showPit != 0;
    }

    private static string ProfitLine(string shelfText, string costText, bool isPitstop)
    {
        var shelf = (shelfText ?? string.Empty).Trim();
        var cost = (costText ?? string.Empty).Trim();
        if (!decimal.TryParse(shelf, NumberStyles.Number, CultureInfo.InvariantCulture, out var s)
            || !decimal.TryParse(cost, NumberStyles.Number, CultureInfo.InvariantCulture, out var c))
        {
            return "-";
        }

        var profit = s - c;
        return isPitstop
            ? $"Pitstop profit: {profit:0.00}"
            : $"Bar profit: {profit:0.00}";
    }

    private static StackPanel Labeled(string caption, UIElement field)
    {
        var sp = new StackPanel { Spacing = 4 };
        sp.Children.Add(
            new TextBlock
            {
                Text = caption,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
            });
        sp.Children.Add(field);
        return sp;
    }

    private static StackPanel Labeled(string caption, TouchFieldRow row) =>
        Labeled(caption, row.Container);

    private static Border BuildEditShotMixerSpiritsCard(EditItemFormState form, StockManagementPageViewModel vm)
    {
        var spiritsBox = new TextBox
        {
            MinHeight = 120,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["PosTouchFieldTextBoxStyle"],
            Text = vm.DetailShotMixerSpiritsText,
            PlaceholderText = "One spirit per line (e.g. Vodka, Rum)",
        };
        form.ShotMixerSpiritsBox = spiritsBox;

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(
            new TextBlock
            {
                Text = "Shot + Mixer spirits",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
            });
        stack.Children.Add(spiritsBox);
        stack.Children.Add(
            CreateEditHintText(
                "Labels shown on the bar Shot + Mixer button. Mixer stock is deducted from the selected mixer product, not these lines."));

        return new Border
        {
            Style = (Style)Application.Current.Resources["PosCardBorderStyle"],
            Padding = new Thickness(14, 12, 14, 12),
            Child = stack,
        };
    }

    private async Task CloseItemEditFullScreenAsync(EditItemFormState? form)
    {
        if (form is not null)
        {
            FlushEditItemFormToViewModel(form);
        }

        if (_itemEditIsNewDraft)
        {
            await _vm.AbandonNewItemDraftIfNeededAsync().ConfigureAwait(true);
            _itemEditIsNewDraft = false;
        }

        CloseFullScreenAfterItemEdit();
    }

    internal async Task OpenAddItemFullScreenAsync()
    {
        if (!await _vm.BeginNewItemDraftAsync().ConfigureAwait(true))
        {
            return;
        }

        _itemEditIsNewDraft = true;
        _vm.DetailShowOnShoppingList = true;
        ShowFullScreen(StockManagementScreen.ItemEdit, BuildEditFullScreenShell(isNewItem: true));
    }

    private void ShowFullScreen(StockManagementScreen screen, UIElement content)
    {
        _vm.CurrentScreen = screen;
        StockFullScreenPresenter.Content = content;
    }

    private void CloseFullScreen()
    {
        StockFullScreenPresenter.Content = null;
        _vm.CurrentScreen = StockManagementScreen.Home;
    }

    private void CloseFullScreenAfterItemEdit()
    {
        if (_resumeProductSetupAfterItemEdit)
        {
            _resumeProductSetupAfterItemEdit = false;
            ShowProductSetupFullScreen();
            return;
        }

        CloseFullScreen();
    }

    internal void OpenItemEditFullScreen()
    {
        if (!_vm.HasSelection)
        {
            return;
        }

        _itemEditIsNewDraft = false;
        ShowFullScreen(StockManagementScreen.ItemEdit, BuildEditFullScreenShell(isNewItem: false));
    }

    internal Task OpenItemEditFullScreenAsync()
    {
        OpenItemEditFullScreen();
        return Task.CompletedTask;
    }

    internal void OpenAdjustStockFullScreen()
    {
        if (!_vm.HasSelection)
        {
            _vm.SetStatusMessage("Select an item from the list first.");
            return;
        }

        var itemTitle = new TextBlock
        {
            Text = _vm.DetailName,
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var qtyLabel = new TextBlock
        {
            Text = _vm.AdjustQuantityText,
            FontSize = 40,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(itemTitle);
        var qtyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };
        var minus = new Button { Content = "-", MinHeight = 56, MinWidth = 72, Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"] };
        minus.Click += (_, _) => { _vm.AdjustQuantityDelta(-1); qtyLabel.Text = _vm.AdjustQuantityText; };
        var plus = new Button { Content = "+", MinHeight = 56, MinWidth = 72, Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"] };
        plus.Click += (_, _) => { _vm.AdjustQuantityDelta(1); qtyLabel.Text = _vm.AdjustQuantityText; };
        qtyRow.Children.Add(minus);
        qtyRow.Children.Add(qtyLabel);
        qtyRow.Children.Add(plus);
        panel.Children.Add(qtyRow);
        var reasonRow = CreateKeyboardRow(_vm.AdjustReason, "Reason (required)");
        reasonRow.TextBox.PlaceholderText = "e.g. Delivery, count correction, waste";
        reasonRow.TextBox.TextChanged += (_, _) => _vm.AdjustReason = reasonRow.TextBox.Text ?? string.Empty;
        panel.Children.Add(Labeled("Adjustment reason", reasonRow));
        var apply = new Button
        {
            Content = "Apply Adjustment",
            MinHeight = 56,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        apply.Click += async (_, _) =>
        {
            _vm.AdjustReason = reasonRow.TextBox.Text ?? string.Empty;
            if (await _vm.ApplySelectedStockAdjustmentAsync().ConfigureAwait(true))
            {
                CloseFullScreen();
            }
        };
        var shell = new Grid { RowSpacing = 8 };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var back = new Button { Content = "Back", MinHeight = 56, Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"] };
        back.Click += (_, _) => CloseFullScreen();
        Grid.SetRow(back, 0);
        shell.Children.Add(back);
        var scroll = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        shell.Children.Add(scroll);
        Grid.SetRow(apply, 2);
        shell.Children.Add(apply);
        ShowFullScreen(StockManagementScreen.AdjustStock, shell);
    }

    internal async Task OpenImportWizardFullScreenAsync()
    {
        var svc = App.Services.GetRequiredService<StockV2ItemImportPreviewService>();
        var body = new StackPanel { Spacing = 10 };
        StockV2ImportFilePreview? lastFilePreview = null;

        var pick = new Button
        {
            Content = "1. Select stock_items.json",
            MinHeight = 56,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        pick.Click += async (_, _) =>
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = _windowHandleProvider.WindowHandle;
            if (hwnd != IntPtr.Zero)
            {
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            lastFilePreview = await svc.PreviewFileForImportAsync(file.Path).ConfigureAwait(true);
            var lastPreview = lastFilePreview.Result;
            body.Children.Clear();
            body.Children.Add(new TextBlock
            {
                Text = "Review warnings and errors before importing.",
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
                TextWrapping = TextWrapping.WrapWholeWords,
            });
            if (lastFilePreview.AlreadyImported)
            {
                body.Children.Add(new TextBlock
                {
                    Text = "This file was imported before (same path and content). Re-import only if you intend to merge or refresh legacy rows.",
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["PosBalanceNegativeBrush"],
                    TextWrapping = TextWrapping.WrapWholeWords,
                });
            }

            body.Children.Add(new TextBlock
            {
                Text = $"{lastPreview.ValidCount} valid   {lastPreview.WarningCount} warnings   {lastPreview.ErrorCount} errors",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            foreach (var issue in lastPreview.Warnings.Concat(lastPreview.Errors).Take(48))
            {
                body.Children.Add(new TextBlock
                {
                    Text = $"{(issue.Severity == StockV2ImportIssueSeverity.Error ? "Error" : "Warning")} - {issue.ItemName}: {issue.Message}",
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Foreground = issue.Severity == StockV2ImportIssueSeverity.Error
                        ? (Brush)Application.Current.Resources["PosBalanceNegativeBrush"]
                        : (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
                });
            }

            var importBtn = new Button
            {
                Content = lastPreview.ErrorCount > 0 ? "Fix errors before import" : "2. Import items",
                MinHeight = 56,
                IsEnabled = lastPreview.ErrorCount == 0 && lastPreview.ValidCount > 0,
                Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
            };
            importBtn.Click += async (_, _) =>
            {
                if (lastFilePreview is null)
                {
                    _vm.SetStatusMessage("Select and preview a file first.");
                    return;
                }

                var preview = lastFilePreview.Result;
                if (preview.ErrorCount > 0)
                {
                    _vm.SetStatusMessage("Fix import errors before committing.");
                    return;
                }

                if (lastFilePreview.AlreadyImported)
                {
                    var dup = new ContentDialog
                    {
                        Title = "Import again?",
                        Content = new TextBlock
                        {
                            Text =
                                "This stock_items.json was already imported with identical content. Import again only if you are refreshing from a backup.",
                            TextWrapping = TextWrapping.WrapWholeWords,
                            MaxWidth = 420,
                        },
                        PrimaryButtonText = "Import again",
                        CloseButtonText = "Cancel",
                        XamlRoot = XamlRoot,
                    };
                    PosContentDialogHelper.ApplyPosStyle(dup);
                    if (await dup.ShowAsync() != ContentDialogResult.Primary)
                    {
                        return;
                    }
                }

                if (preview.WarningCount > 0)
                {
                    var confirm = new ContentDialog
                    {
                        Title = "Import with warnings",
                        Content = new TextBlock
                        {
                            Text = $"{preview.WarningCount} warning(s) were found. Import {preview.ValidCount} item(s) anyway?",
                            TextWrapping = TextWrapping.WrapWholeWords,
                            MaxWidth = 420,
                        },
                        PrimaryButtonText = "Import",
                        CloseButtonText = "Cancel",
                        XamlRoot = XamlRoot,
                    };
                    PosContentDialogHelper.ApplyPosStyle(confirm);
                    if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                    {
                        return;
                    }
                }

                var json = await System.IO.File.ReadAllTextAsync(lastFilePreview.SourcePath).ConfigureAwait(true);
                var items = StockV2ItemImportPreviewService.ParseStockItemsJson(json);
                await svc
                    .CommitImportAsync(lastFilePreview.SourcePath, lastFilePreview.ContentSha256Hex, items)
                    .ConfigureAwait(true);
                await _vm.LoadAsync().ConfigureAwait(true);
                _vm.PulseCatalogRefresh();
                App.Services.GetRequiredService<PosCatalogAutoRefreshService>().PulseRefresh();
                _vm.SetStatusMessage($"Imported {items.Count} item(s).");
                CloseFullScreen();
            };
            body.Children.Add(importBtn);
        };
        body.Children.Add(pick);
        var shell = new Grid { RowSpacing = 8 };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var hdr = new TextBlock { Text = "Import wizard", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        var back = new Button { Content = "Back", MinHeight = 56, Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"] };
        back.Click += (_, _) => CloseFullScreen();
        Grid.SetRow(hdr, 0);
        shell.Children.Add(hdr);
        Grid.SetRow(back, 0);
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        shell.Children.Add(scroll);
        ShowFullScreen(StockManagementScreen.ImportWizard, shell);
        await Task.CompletedTask;
    }

    private sealed class EditItemFormState
    {
        public TouchFieldRow? NameRow;
        public TouchFieldRow? SkuRow;
        public ComboBox? BucketBox;
        public ComboBox? SubBox;
        public TouchFieldRow? BarRow;
        public TouchFieldRow? PitRow;
        public TouchFieldRow? GuestRow;
        public TouchFieldRow? CostRow;
        public ToggleSwitch? OpenPriceSwitch;
        public TextBlock? BarProfitText;
        public TextBlock? PitProfitText;
        public TextBlock? OpenPriceHintText;
        public ToggleSwitch? SpecialEnabledSwitch;
        public TouchFieldRow? SpecialBarRow;
        public TouchFieldRow? SpecialGuestRow;
        public TouchFieldRow? SpecialPitRow;
        public ComboBox? StockModeBox;
        public TouchFieldRow? StockQtyRow;
        public TouchFieldRow? LowStockRow;
        public TouchFieldRow? ParLevelRow;
        public TouchFieldRow? PackSizeRow;
        public ToggleSwitch? ActiveSwitch;
        public TouchFieldRow? NotesRow;
        public TextBox? ShotMixerSpiritsBox;
        public readonly List<TextBlock> PreviewBadgeTexts = new();
        public readonly List<TextBlock> PreviewBarPriceTexts = new();
        public readonly List<TextBlock> PreviewPitPriceTexts = new();
        public readonly List<TextBlock> PreviewChannelTexts = new();
        public readonly List<TextBlock> PreviewStockTexts = new();
        public TextBlock? BarPriceCaptionText;
    }

    private UIElement BuildEditFullScreenShell(bool isNewItem)
    {
        var form = new EditItemFormState();
        var isShotMixer = _vm.IsShotMixerDetail;
        var tabLabels = new[] { "Basic", "Prices", "Stock", "Advanced" };
        var panels = new UIElement[]
        {
            BuildEditBasicPanel(form),
            BuildEditPricesPanel(form, isShotMixer),
            BuildEditStockPanel(form),
            BuildEditAdvancedPanel(form, isShotMixer),
        };

        var contentHost = new Grid();
        for (var i = 0; i < panels.Length; i++)
        {
            panels[i].Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed;
            contentHost.Children.Add(panels[i]);
        }

        var tabButtons = new Button[tabLabels.Length];
        var accentBrush = (Brush)Application.Current.Resources["PosAccentBrush"];
        var surfaceAlt = (Brush)Application.Current.Resources["PosSurfaceAltBrush"];
        var borderBrush = (Brush)Application.Current.Resources["PosBorderBrush"];

        void SetTabSelected(int index)
        {
            for (var i = 0; i < tabButtons.Length; i++)
            {
                var selected = i == index;
                tabButtons[i].Background = selected
                    ? accentBrush
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                tabButtons[i].Foreground = selected
                    ? new SolidColorBrush(Microsoft.UI.Colors.White)
                    : (Brush)Application.Current.Resources["PosTextPrimaryBrush"];
            }

            for (var i = 0; i < panels.Length; i++)
            {
                panels[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        void SelectTab(int index)
        {
            FlushEditItemFormToViewModel(form);
            SetTabSelected(index);
            RefreshEditPreview(form);
        }

        var tabGrid = new Grid();
        for (var i = 0; i < tabLabels.Length; i++)
        {
            tabGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var i = 0; i < tabLabels.Length; i++)
        {
            var idx = i;
            var btn = new Button
            {
                Content = tabLabels[i],
                MinHeight = 44,
                Margin = new Thickness(3),
                Padding = new Thickness(8, 8, 8, 8),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(10),
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["PosTextPrimaryBrush"],
            };
            btn.Click += (_, _) => SelectTab(idx);
            tabButtons[i] = btn;
            Grid.SetColumn(btn, i);
            tabGrid.Children.Add(btn);
        }

        SetTabSelected(0);
        WireEditItemLivePreview(form);

        var tabStrip = new Border
        {
            Background = surfaceAlt,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(4),
            Child = tabGrid,
        };

        var scroll = new ScrollViewer
        {
            Content = contentHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 4, 0, 8),
        };

        var back = new Button
        {
            Content = _resumeProductSetupAfterItemEdit ? "Back to product setup" : "Back to stock",
            MinHeight = 44,
            MinWidth = 128,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        back.Click += async (_, _) => await CloseItemEditFullScreenAsync(form).ConfigureAwait(true);

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(back, 0);
        header.Children.Add(back);
        var titleStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(
            new TextBlock
            {
                Text = isNewItem ? "New item" : (string.IsNullOrWhiteSpace(_vm.DetailName) ? "Edit item" : _vm.DetailName),
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["PosTextPrimaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        titleStack.Children.Add(
            new TextBlock
            {
                Text = _resumeProductSetupAfterItemEdit
                    ? (isNewItem
                        ? "Advanced product setup - add bar, pitstop, guest, specials, and catalog fields"
                        : "Advanced product setup - bar, pitstop, guest, specials, barcodes, and catalog")
                    : (isNewItem
                        ? "Enter details, then save to add to the catalog"
                        : "Edit item details, prices, stock and visibility"),
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
            });
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        var footer = new Grid
        {
            ColumnSpacing = 10,
            Padding = new Thickness(16, 12, 16, 12),
            Background = (Brush)Application.Current.Resources["PosSurfaceBrush"],
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var del = new Button
        {
            Content = "Delete",
            MinHeight = 48,
            MinWidth = 108,
            Visibility = isNewItem ? Visibility.Collapsed : Visibility.Visible,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        del.Click += async (_, _) =>
        {
            FlushEditItemFormToViewModel(form);
            _itemEditIsNewDraft = false;
            CloseFullScreenAfterItemEdit();
            await DeleteItem_Click_ImplAsync().ConfigureAwait(true);
        };

        var cancel = new Button
        {
            Content = "Cancel",
            MinHeight = 48,
            MinWidth = 108,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        cancel.Click += async (_, _) => await CloseItemEditFullScreenAsync(form).ConfigureAwait(true);

        var save = new Button
        {
            Content = isNewItem ? "Save new item" : "Save changes",
            MinHeight = 48,
            MinWidth = 148,
            Style = (Style)Application.Current.Resources["SettingsPrimaryButtonStyle"],
        };
        save.Click += async (_, _) =>
        {
            FlushEditItemFormToViewModel(form);
            if (await _vm.TryPersistCurrentItemAsync().ConfigureAwait(true))
            {
                _itemEditIsNewDraft = false;
                CloseFullScreenAfterItemEdit();
            }
        };

        Grid.SetColumn(del, 0);
        footer.Children.Add(del);
        Grid.SetColumn(cancel, 2);
        footer.Children.Add(cancel);
        Grid.SetColumn(save, 3);
        footer.Children.Add(save);

        var inner = new Grid { RowSpacing = 12, Padding = new Thickness(16, 14, 16, 0) };
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(header, 0);
        inner.Children.Add(header);
        Grid.SetRow(tabStrip, 1);
        inner.Children.Add(tabStrip);
        Grid.SetRow(scroll, 2);
        inner.Children.Add(scroll);
        Grid.SetRow(footer, 3);
        inner.Children.Add(footer);

        return new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 253, 254, 255)),
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = inner,
        };
    }

    private UIElement BuildEditBasicPanel(EditItemFormState form)
    {
        form.NameRow = CreateKeyboardRow(_vm.DetailName, "Item name");
        form.SkuRow = CreateKeyboardRow(_vm.DetailSku, "Barcode / SKU");
        (form.BucketBox, form.SubBox) = CreateCatalogBucketSubPickers(_vm.DetailCatalogBucket, _vm.DetailCatalogSubCategory);
        form.BucketBox.SelectionChanged += (_, _) =>
        {
            SyncEditVisibilityFromBucket(form);
            RefreshEditPreview(form);
        };

        var catalogCard = CreateEditBucketCatalogCard(form);
        var summaryCard = CreateEditVisibilityPreviewCard(form, "Pricing preview");

        var left = new StackPanel { Spacing = 14 };
        left.Children.Add(Labeled("Name", form.NameRow));
        left.Children.Add(Labeled("Barcode", form.SkuRow));
        left.Children.Add(Labeled("Bucket", form.BucketBox));
        left.Children.Add(Labeled("Sub-category", form.SubBox));
        left.Children.Add(catalogCard);

        var right = new StackPanel { Spacing = 14 };
        right.Children.Add(Labeled("Product image", CreateProductImagePickerBlock(persistOnPick: false)));
        right.Children.Add(summaryCard);

        var grid = new Grid { ColumnSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return WrapEditPanel(grid);
    }

    private UIElement BuildEditPricesPanel(EditItemFormState form, bool isShotMixer)
    {
        var barCaption = isShotMixer ? "Shot + mixer price" : "Bar price";
        form.BarRow = CreateEditMoneyRow(_vm.BarPriceText, barCaption);
        form.BarRow.TextBox.PlaceholderText = isShotMixer ? "Member tab price" : "0.00 = free at bar";
        form.PitRow = CreateEditMoneyRow(_vm.PitstopPriceText, "Pitstop / retail price");
        form.PitRow.TextBox.PlaceholderText = "Leave blank if not sold in Pitstop";
        form.GuestRow = CreateEditMoneyRow(_vm.DetailGuestPriceText, "Guest tab price");
        form.GuestRow.TextBox.PlaceholderText = "Optional";
        form.CostRow = CreateEditMoneyRow(_vm.CostPriceText, "Cost price");

        form.BarProfitText = CreateEditProfitTextBlock();
        form.PitProfitText = CreateEditProfitTextBlock();
        void RefreshProfits()
        {
            form.BarProfitText.Text = ProfitLine(form.BarRow.TextBox.Text, form.CostRow.TextBox.Text, isPitstop: false);
            form.PitProfitText.Text = ProfitLine(form.PitRow.TextBox.Text, form.CostRow.TextBox.Text, isPitstop: true);
        }

        form.BarRow.TextBox.TextChanged += (_, _) => RefreshProfits();
        form.PitRow.TextBox.TextChanged += (_, _) => RefreshProfits();
        form.GuestRow.TextBox.TextChanged += (_, _) => RefreshProfits();
        form.CostRow.TextBox.TextChanged += (_, _) => RefreshProfits();
        RefreshProfits();

        form.OpenPriceSwitch = new ToggleSwitch
        {
            Header = "Open price at bar (enter amount each sale)",
            IsOn = _vm.DetailUsesOpenPrice,
            OnContent = "On",
            OffContent = "Off",
        };
        form.OpenPriceHintText = CreateEditHintText(
            "When on, staff use the numpad at the bar for each pour. You can still set a hint price below, or leave it blank.");
        form.BarPriceCaptionText = new TextBlock
        {
            Text = barCaption,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
        };
        form.OpenPriceSwitch.Toggled += (_, _) => UpdateOpenPriceFieldHints(form, isShotMixer);

        var panel = new StackPanel { Spacing = 16, MaxWidth = 900 };
        panel.Children.Add(CreateEditVisibilityPreviewCard(form, "Shelf summary"));
        panel.Children.Add(
            CreateEditHintText(
                isShotMixer
                    ? "Member tabs use shot + mixer price. Guest tabs use guest price when set."
                    : "Enter 0.00 for free bar pours. Open price lets staff enter the amount on each sale."));

        if (isShotMixer)
        {
            panel.Children.Add(Labeled(barCaption, form.BarRow));
            panel.Children.Add(form.BarProfitText);
            panel.Children.Add(Labeled("Guest tab price", form.GuestRow));
        }
        else
        {
            var barPitRow = new Grid { ColumnSpacing = 16 };
            barPitRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barPitRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var barCol = new StackPanel { Spacing = 6 };
            barCol.Children.Add(form.BarPriceCaptionText);
            barCol.Children.Add(form.BarRow.Container);
            barCol.Children.Add(form.BarProfitText);
            var pitCol = new StackPanel { Spacing = 6 };
            pitCol.Children.Add(Labeled("Pitstop / retail", form.PitRow));
            pitCol.Children.Add(form.PitProfitText);
            Grid.SetColumn(barCol, 0);
            barPitRow.Children.Add(barCol);
            Grid.SetColumn(pitCol, 1);
            barPitRow.Children.Add(pitCol);
            panel.Children.Add(barPitRow);
            panel.Children.Add(Labeled("Guest tab price", form.GuestRow));
            panel.Children.Add(Labeled("Cost price", form.CostRow));

            var openCard = new Border
            {
                Style = (Style)Application.Current.Resources["PosCardBorderStyle"],
                Padding = new Thickness(14, 12, 14, 12),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children = { form.OpenPriceSwitch, form.OpenPriceHintText },
                },
            };
            panel.Children.Add(openCard);
        }

        form.SpecialEnabledSwitch = new ToggleSwitch
        {
            Header = "Promotional special pricing",
            IsOn = _vm.SpecialEnabled,
            OnContent = "On",
            OffContent = "Off",
        };
        form.SpecialBarRow = CreateEditMoneyRow(_vm.DetailBarSpecialText, "Special bar price");
        form.SpecialGuestRow = CreateEditMoneyRow(_vm.DetailGuestSpecialText, "Special guest price");
        form.SpecialPitRow = CreateEditMoneyRow(_vm.DetailPitstopSpecialText, "Special pitstop price");

        var specialCard = new Border
        {
            Style = (Style)Application.Current.Resources["PosCardBorderStyle"],
            Padding = new Thickness(16, 14, 16, 14),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    form.SpecialEnabledSwitch,
                    CreateEditHintText("When enabled, special prices override regular shelf prices for the selected channels."),
                    Labeled("Special bar", form.SpecialBarRow),
                    Labeled("Special guest", form.SpecialGuestRow),
                    Labeled(isShotMixer ? "Special pitstop (unused)" : "Special pitstop", form.SpecialPitRow),
                },
            },
        };
        panel.Children.Add(specialCard);
        UpdateOpenPriceFieldHints(form, isShotMixer);
        return WrapEditPanel(panel);
    }

    private UIElement BuildEditStockPanel(EditItemFormState form)
    {
        form.StockModeBox = CreateStockModePicker(_vm.DetailStockMode);
        form.StockQtyRow = CreateEditIntegerRow(_vm.DetailStockText, "Current stock", 0, 999999);
        form.LowStockRow = CreateEditIntegerRow(_vm.DetailWarnMeBelowText, "Warn Me Below", 0, 999999);
        form.ParLevelRow = CreateEditIntegerRow(_vm.DetailPreferredStockLevelText, "Target Amount", 0, 999999);
        form.PackSizeRow = CreateEditIntegerRow(_vm.DetailPurchaseUnitQtyText, "Pack size", 1, 9999);
        var stockStatus = new TextBlock
        {
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["PosTextPrimaryBrush"],
        };
        form.PreviewStockTexts.Add(stockStatus);
        var statusCard = CreateEditSummaryCard("Stock status", stockStatus, null);

        var qtyGrid = new Grid { ColumnSpacing = 12 };
        qtyGrid.ColumnDefinitions.Add(new ColumnDefinition());
        qtyGrid.ColumnDefinitions.Add(new ColumnDefinition());
        qtyGrid.ColumnDefinitions.Add(new ColumnDefinition());
        var onHandCol = Labeled("On hand", form.StockQtyRow);
        var lowAtCol = Labeled("Warn below", form.LowStockRow);
        var parCol = Labeled("Target", form.ParLevelRow);
        Grid.SetColumn(onHandCol, 0);
        qtyGrid.Children.Add(onHandCol);
        Grid.SetColumn(lowAtCol, 1);
        qtyGrid.Children.Add(lowAtCol);
        Grid.SetColumn(parCol, 2);
        qtyGrid.Children.Add(parCol);

        var panel = new StackPanel { Spacing = 14, MaxWidth = 820 };
        panel.Children.Add(statusCard);
        panel.Children.Add(Labeled("How we stock this item", form.StockModeBox));
        panel.Children.Add(
            CreateEditHintText(
                "Tracked - count stock and reorder when low. Sell until gone - run down, no buying. Order only - not kept on shelf. Not counted - skip stock checks."));
        panel.Children.Add(qtyGrid);
        panel.Children.Add(Labeled("Pack size", form.PackSizeRow));
        panel.Children.Add(
            CreateEditHintText(
                "Units per pack or case when ordering (e.g. 24). Shopping list uses this for suggested buy amounts."));
        panel.Children.Add(
            CreateEditHintText(
                "For stock adjustments with a quantity change, use Adjust stock from the list - a reason is required there."));
        return WrapEditPanel(panel);
    }

    private UIElement BuildEditAdvancedPanel(EditItemFormState form, bool isShotMixer)
    {
        form.ActiveSwitch = new ToggleSwitch
        {
            Header = "Item active in catalog",
            IsOn = _vm.DetailIsActive,
            OnContent = "Active",
            OffContent = "Inactive",
        };
        form.NotesRow = CreateKeyboardRow(_vm.DetailNotesText, "Internal notes");

        var panel = new StackPanel { Spacing = 14, MaxWidth = 720 };
        if (isShotMixer)
        {
            panel.Children.Add(BuildEditShotMixerSpiritsCard(form, _vm));
            panel.Children.Add(
                CreateEditHintText(
                    "Use the exact name \"Shot + Mixer\" (or item type ShotMixer) so the bar recognizes this configuration."));
        }

        panel.Children.Add(form.ActiveSwitch);
        panel.Children.Add(Labeled("Notes", form.NotesRow));
        panel.Children.Add(
            CreateEditHintText("Inactive items stay in the database but are hidden from normal stock filters."));
        return WrapEditPanel(panel);
    }

    private static Border WrapEditPanel(UIElement content) =>
        new()
        {
            Padding = new Thickness(0, 8, 0, 24),
            Child = content,
        };

    private static string DescribeBucketCatalogPlacement(string? bucket)
    {
        return StockCatalogTaxonomy.NormalizeBucket(bucket) switch
        {
            StockCatalogTaxonomy.BucketShared =>
                "Bar tabs and Pitstop - one item, shared stock and category rules",
            StockCatalogTaxonomy.BucketPitstop =>
                "Pitstop only - retail shelf, not on bar tabs",
            _ =>
                "Bar tabs only - poured/served at the bar",
        };
    }

    private void SyncEditVisibilityFromBucket(EditItemFormState form)
    {
        var bucket = form.BucketBox?.SelectedItem as string ?? _vm.DetailCatalogBucket;
        ApplyCatalogBucketToVmVisibility(bucket, _vm);
    }

    private static Border CreateEditBucketCatalogCard(EditItemFormState form)
    {
        var channelLine = new TextBlock
        {
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["PosTextPrimaryBrush"],
        };
        form.PreviewChannelTexts.Add(channelLine);

        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(
            new TextBlock
            {
                Text = "Where it appears",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
            });
        stack.Children.Add(channelLine);
        stack.Children.Add(
            CreateEditHintText(
                "Bucket chooses Bar, Pitstop, or Shared. Sub-category is the shelf group inside that bucket."));

        return new Border
        {
            Style = (Style)Application.Current.Resources["PosCardBorderStyle"],
            Padding = new Thickness(14, 12, 14, 12),
            Child = stack,
        };
    }

    private static Border CreateEditVisibilityPreviewCard(EditItemFormState form, string title)
    {
        var badge = new TextBlock
        {
            FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["PosTextPrimaryBrush"],
        };
        var barLine = new TextBlock
        {
            FontSize = 14,
            Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
        };
        var pitLine = new TextBlock
        {
            FontSize = 14,
            Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
        };
        form.PreviewBadgeTexts.Add(badge);
        form.PreviewBarPriceTexts.Add(barLine);
        form.PreviewPitPriceTexts.Add(pitLine);

        var lines = new StackPanel { Spacing = 6 };
        lines.Children.Add(badge);
        lines.Children.Add(barLine);
        lines.Children.Add(pitLine);

        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(
            new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
            });
        stack.Children.Add(lines);

        return new Border
        {
            Style = (Style)Application.Current.Resources["PosCardBorderStyle"],
            Padding = new Thickness(14, 12, 14, 12),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 248, 250, 252)),
            Child = stack,
        };
    }

    private static TextBlock CreateEditHintText(string text) =>
        new()
        {
            Text = text,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
            TextWrapping = TextWrapping.WrapWholeWords,
        };

    private static TextBlock CreateEditProfitTextBlock() =>
        new()
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["PosTextPrimaryBrush"],
        };

    private static Border CreateEditSummaryCard(string title, TextBlock primary, TextBlock? secondary)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(
            new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
            });
        stack.Children.Add(primary);
        if (secondary is not null)
        {
            stack.Children.Add(secondary);
        }

        return new Border
        {
            Style = (Style)Application.Current.Resources["PosCardBorderStyle"],
            Padding = new Thickness(16, 12, 16, 12),
            Child = stack,
        };
    }

    private void WireEditItemLivePreview(EditItemFormState form)
    {
        void Hook(TextBox? tb)
        {
            if (tb is null)
            {
                return;
            }

            tb.TextChanged += (_, _) => RefreshEditPreview(form);
        }

        Hook(form.NameRow?.TextBox);
        Hook(form.BarRow?.TextBox);
        Hook(form.PitRow?.TextBox);
        Hook(form.GuestRow?.TextBox);
        Hook(form.CostRow?.TextBox);
        if (form.OpenPriceSwitch is not null)
        {
            form.OpenPriceSwitch.Toggled += (_, _) => RefreshEditPreview(form);
        }

        if (form.StockModeBox is not null)
        {
            form.StockModeBox.SelectionChanged += (_, _) =>
            {
                FlushEditItemFormToViewModel(form);
                RefreshEditPreview(form);
            };
        }

        Hook(form.StockQtyRow?.TextBox);
        Hook(form.PackSizeRow?.TextBox);
        SyncEditVisibilityFromBucket(form);
        RefreshEditPreview(form);
    }

    private void UpdateOpenPriceFieldHints(EditItemFormState form, bool isShotMixer)
    {
        if (isShotMixer || form.OpenPriceSwitch is null || form.BarPriceCaptionText is null)
        {
            return;
        }

        var open = form.OpenPriceSwitch.IsOn;
        form.BarPriceCaptionText.Text = open ? "Open price hint (optional)" : "Bar price";
        if (form.BarRow is not null)
        {
            form.BarRow.TextBox.PlaceholderText = open
                ? "Optional suggested amount"
                : "0.00 = free at bar";
        }

        if (form.OpenPriceHintText is not null)
        {
            form.OpenPriceHintText.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void FlushEditItemFormToViewModel(EditItemFormState form)
    {
        if (form.NameRow is not null)
        {
            _vm.DetailName = form.NameRow.TextBox.Text ?? string.Empty;
        }

        if (form.SkuRow is not null)
        {
            _vm.DetailSku = form.SkuRow.TextBox.Text ?? string.Empty;
        }

        if (form.BucketBox is not null)
        {
            _vm.DetailCatalogBucket = form.BucketBox.SelectedItem as string ?? _vm.DetailCatalogBucket;
            ApplyCatalogBucketToVmVisibility(_vm.DetailCatalogBucket, _vm);
        }

        if (form.SubBox is not null)
        {
            _vm.DetailCatalogSubCategory = form.SubBox.SelectedItem as string ?? _vm.DetailCatalogSubCategory;
        }

        if (form.BarRow is not null)
        {
            _vm.BarPriceText = form.BarRow.TextBox.Text ?? string.Empty;
        }

        if (form.PitRow is not null)
        {
            _vm.PitstopPriceText = form.PitRow.TextBox.Text ?? string.Empty;
        }

        if (form.GuestRow is not null)
        {
            _vm.DetailGuestPriceText = form.GuestRow.TextBox.Text ?? string.Empty;
        }

        if (form.CostRow is not null)
        {
            _vm.CostPriceText = form.CostRow.TextBox.Text ?? string.Empty;
        }

        if (form.OpenPriceSwitch is not null)
        {
            _vm.DetailUsesOpenPrice = form.OpenPriceSwitch.IsOn;
        }

        if (form.SpecialEnabledSwitch is not null)
        {
            _vm.SpecialEnabled = form.SpecialEnabledSwitch.IsOn;
            _vm.DetailIsOnSpecial = form.SpecialEnabledSwitch.IsOn;
        }

        if (form.SpecialBarRow is not null)
        {
            _vm.DetailBarSpecialText = form.SpecialBarRow.TextBox.Text ?? string.Empty;
        }

        if (form.SpecialGuestRow is not null)
        {
            _vm.DetailGuestSpecialText = form.SpecialGuestRow.TextBox.Text ?? string.Empty;
        }

        if (form.SpecialPitRow is not null)
        {
            _vm.DetailPitstopSpecialText = form.SpecialPitRow.TextBox.Text ?? string.Empty;
        }

        if (form.StockModeBox is not null)
        {
            _vm.DetailStockMode = form.StockModeBox.SelectedItem is ComboBoxItem { Tag: string mode }
                ? mode
                : form.StockModeBox.SelectedItem as string ?? _vm.DetailStockMode;
            _vm.SyncDetailFlagsFromStockMode();
        }

        if (form.StockQtyRow is not null)
        {
            _vm.DetailStockText = form.StockQtyRow.TextBox.Text ?? string.Empty;
        }

        if (form.LowStockRow is not null)
        {
            var warn = form.LowStockRow.TextBox.Text ?? string.Empty;
            _vm.DetailWarnMeBelowText = warn;
            _vm.LowStockThresholdText = warn;
        }

        if (form.ParLevelRow is not null)
        {
            var pref = form.ParLevelRow.TextBox.Text ?? string.Empty;
            _vm.DetailPreferredStockLevelText = pref;
            _vm.DetailParLevelText = pref;
        }

        if (form.PackSizeRow is not null)
        {
            _vm.DetailPurchaseUnitQtyText = form.PackSizeRow.TextBox.Text ?? string.Empty;
        }

        if (form.ActiveSwitch is not null)
        {
            _vm.DetailIsActive = form.ActiveSwitch.IsOn;
        }

        if (form.NotesRow is not null)
        {
            _vm.DetailNotesText = form.NotesRow.TextBox.Text ?? string.Empty;
        }

        if (form.ShotMixerSpiritsBox is not null)
        {
            _vm.DetailShotMixerSpiritsText = form.ShotMixerSpiritsBox.Text ?? string.Empty;
        }
    }

    private void RefreshEditPreview(EditItemFormState form)
    {
        FlushEditItemFormToViewModel(form);
        var badge = _vm.HeaderVisibilityBadge;
        var bar = $"Bar: {_vm.DisplayBarPrice}";
        var pit = $"Pitstop: {_vm.DisplayPitstopPrice}";
        foreach (var t in form.PreviewBadgeTexts)
        {
            t.Text = badge;
        }

        foreach (var t in form.PreviewBarPriceTexts)
        {
            t.Text = bar;
        }

        foreach (var t in form.PreviewPitPriceTexts)
        {
            t.Text = pit;
        }

        foreach (var t in form.PreviewStockTexts)
        {
            t.Text = _vm.StockStatusPreview;
        }

        var channelText = DescribeBucketCatalogPlacement(_vm.DetailCatalogBucket);
        foreach (var t in form.PreviewChannelTexts)
        {
            t.Text = channelText;
        }
    }

    private async Task DeleteItem_Click_ImplAsync()
    {
        if (!_vm.HasSelection)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(_vm.DetailName) ? "this item" : _vm.DetailName.Trim();
        var dlg = new ContentDialog
        {
            Title = "Delete item",
            Content = new TextBlock { Text = $"Permanently delete \"{name}\"?", TextWrapping = TextWrapping.WrapWholeWords, MaxWidth = 420 },
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        PosContentDialogHelper.ApplyPosStyle(dlg);
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            await _vm.DeleteSelectedItemAsync().ConfigureAwait(true);
        }
    }
}
