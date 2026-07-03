using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace NickeltownPOSV4.Views;

public sealed partial class StockManagementPage
{
    private static Style HeaderButtonStyle =>
        (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"];

    private static Brush PosSurfaceBrush =>
        (Brush)Application.Current.Resources["PosSurfaceBrush"];

    private static Brush PosBorderBrush =>
        (Brush)Application.Current.Resources["PosBorderBrush"];

    private static Style PosCardBorderStyle =>
        (Style)Application.Current.Resources["PosCardBorderStyle"];

    private static Brush PosTextSecondaryBrush =>
        (Brush)Application.Current.Resources["PosTextSecondaryBrush"];

    private Border CreateWorkflowCard(UIElement content) =>
        new()
        {
            Style = PosCardBorderStyle,
            Padding = new Thickness(14, 12, 14, 12),
            Child = content,
        };

    private static TextBlock CreateWorkflowSectionTitle(string text) =>
        new()
        {
            Text = text,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
        };

    private static TextBlock CreateWorkflowHeroValue(string text) =>
        new()
        {
            Text = text,
            FontSize = 36,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

    internal void OpenShoppingListFullScreen() => ShowFullScreen(StockManagementScreen.ShoppingList, BuildShoppingListShell());
    internal void OpenReceiveStockFullScreen() => ShowFullScreen(StockManagementScreen.ReceiveStock, BuildReceiveStockShell());
    internal void OpenCountStockFullScreen() => ShowFullScreen(StockManagementScreen.CountStock, BuildCountStockShell());
    internal async void OpenProductsFullScreen()
    {
        await _vm.LoadAsync().ConfigureAwait(true);
        ShowProductSetupFullScreen();
    }

    private void ShowProductSetupFullScreen() =>
        ShowFullScreen(StockManagementScreen.Products, BuildProductSetupShell());

    internal void OpenAdvancedProductEditFromSetup()
    {
        if (!_vm.HasSelection)
        {
            return;
        }

        _resumeProductSetupAfterItemEdit = true;
        OpenItemEditFullScreen();
    }

    internal async Task OpenAddProductFromSetupAsync()
    {
        _resumeProductSetupAfterItemEdit = true;
        await OpenAddItemFullScreenAsync().ConfigureAwait(true);
    }

    private UIElement BuildShoppingListShell()
    {
        var regular = _vm.BuildShoppingListRegularRows();
        var merch = _vm.BuildShoppingListMerchRows();
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(BuildShoppingListSummaryBanner(regular, merch));

        if (regular.Count == 0 && merch.Count == 0)
        {
            body.Children.Add(new Border
            {
                Background = PosSurfaceBrush,
                BorderBrush = PosBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 20, 16, 20),
                Child = new TextBlock
                {
                    Text = "Nothing needs buying right now.",
                    FontSize = 15,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.75,
                },
            });
        }
        else
        {
            AppendShoppingListSection(body, "Bar & supplies", "Regular restock — drinks, food, and bar items.", regular);
            AppendShoppingListSection(body, "Merchandise", "Ordered less often — hats, shirts, and club merch.", merch);
        }

        var copyBtn = WorkflowButton("Copy List");
        copyBtn.Click += (_, _) =>
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(_vm.BuildShoppingListClipboardText());
            Clipboard.SetContent(package);
            _vm.SetStatusMessage("Shopping list copied.");
        };

        var emailBarBtn = WorkflowButton("Email Bar PDF");
        emailBarBtn.IsEnabled = regular.Count > 0;
        emailBarBtn.Click += async (_, _) =>
            await EmailShoppingListPdfAsync(regular, ShoppingListPdfScope.Regular).ConfigureAwait(true);

        var emailMerchBtn = WorkflowButton("Email Merch PDF");
        emailMerchBtn.IsEnabled = merch.Count > 0;
        emailMerchBtn.Click += async (_, _) =>
            await EmailShoppingListPdfAsync(merch, ShoppingListPdfScope.Merch).ConfigureAwait(true);

        var actions = new StackPanel { Spacing = 8 };
        actions.Children.Add(copyBtn);

        var emailRow = new Grid { ColumnSpacing = 8 };
        emailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        emailRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        emailRow.Children.Add(emailBarBtn);
        emailRow.Children.Add(emailMerchBtn);
        Grid.SetColumn(emailMerchBtn, 1);
        actions.Children.Add(emailRow);

        return WorkflowShellWithActions("Shopping List", "What to buy", body, actions);
    }

    private void AppendShoppingListSection(
        StackPanel body,
        string title,
        string subtitle,
        IReadOnlyList<StockShoppingListRowViewModel> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        body.Children.Add(BuildShoppingListSectionHeader(title, subtitle, rows.Count));
        foreach (var row in rows)
        {
            body.Children.Add(BuildShoppingListRowCard(row));
        }
    }

    private static Border BuildShoppingListSectionHeader(string title, string subtitle, int count)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock
        {
            Text = title + "  (" + count + ")",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        return new Border
        {
            Margin = new Thickness(0, 4, 0, 0),
            Padding = new Thickness(0, 4, 0, 0),
            Child = stack,
        };
    }

    private static Border BuildShoppingListSummaryBanner(
        IReadOnlyList<StockShoppingListRowViewModel> regular,
        IReadOnlyList<StockShoppingListRowViewModel> merch)
    {
        var rows = regular.Concat(merch).ToList();
        var outOfStock = rows.Count(r => r.Status == StockVolunteerStatus.OutOfStock);
        var setupWarnings = rows.Count(r => r.HasSetupWarning);
        var totalNeed = rows.Sum(r => r.NeedQty);

        var parts = new List<string>();
        if (rows.Count == 0)
        {
            parts.Add("All stocked up");
        }
        else
        {
            if (regular.Count > 0)
            {
                parts.Add(regular.Count + " bar & supplies");
            }

            if (merch.Count > 0)
            {
                parts.Add(merch.Count + " merch");
            }

            if (totalNeed > 0)
            {
                parts.Add(totalNeed + " units needed");
            }

            if (outOfStock > 0)
            {
                parts.Add(outOfStock + " out of stock");
            }

            if (setupWarnings > 0)
            {
                parts.Add(setupWarnings + " need pack size");
            }
        }

        return new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 238, 242, 255)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 191, 219, 254)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10),
            Child = new TextBlock
            {
                Text = string.Join("  ·  ", parts),
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 30, 58, 138)),
                TextWrapping = TextWrapping.WrapWholeWords,
            },
        };
    }

    private Border BuildShoppingListRowCard(StockShoppingListRowViewModel row)
    {
        var accentBrush = ParseAccentBrush(row.StatusAccentColor);
        var pillBg = ParseAccentBrush(row.StatusPillBackground);

        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleStack = new StackPanel { Spacing = 2 };
        titleStack.Children.Add(new TextBlock
        {
            Text = row.Name,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        if (!string.IsNullOrWhiteSpace(row.CategoryLine))
        {
            titleStack.Children.Add(new TextBlock
            {
                Text = row.CategoryLine,
                FontSize = 12,
                Opacity = 0.65,
            });
        }

        titleRow.Children.Add(titleStack);

        var pill = new Border
        {
            Background = pillBg,
            BorderBrush = accentBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = row.StatusText,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = accentBrush,
            },
        };
        Grid.SetColumn(pill, 1);
        titleRow.Children.Add(pill);

        var metrics = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
        var haveTile = CreateMetricTile("Have", row.HaveQty.ToString(CultureInfo.InvariantCulture), false);
        var needTile = CreateMetricTile("Need", row.NeedQty.ToString(CultureInfo.InvariantCulture), true);
        metrics.Children.Add(haveTile);
        metrics.Children.Add(needTile);
        Grid.SetColumn(needTile, 1);

        var suggestedText = row.HasSetupWarning
            ? "Pack size not set"
            : (string.IsNullOrEmpty(row.SuggestedLine) ? "—" : row.SuggestedLine);
        var suggestedTile = CreateMetricTile("Suggested", suggestedText, false, row.HasSetupWarning);
        Grid.SetColumn(suggestedTile, 2);
        metrics.Children.Add(suggestedTile);

        var card = new StackPanel { Spacing = 0 };
        card.Children.Add(titleRow);
        card.Children.Add(metrics);

        if (!string.IsNullOrEmpty(row.SetupHint))
        {
            card.Children.Add(new TextBlock
            {
                Text = row.SetupHint,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0),
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.IndianRed),
            });
        }

        return new Border
        {
            Background = PosSurfaceBrush,
            BorderBrush = PosBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 10, 12, 10),
            Child = card,
        };
    }

    private static Border CreateMetricTile(string label, string value, bool emphasize, bool warn = false)
    {
        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = emphasize ? 18 : 15,
            FontWeight = emphasize
                ? Microsoft.UI.Text.FontWeights.Bold
                : Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
        };
        if (warn)
        {
            valueBlock.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 180, 83, 9));
        }

        var sp = new StackPanel { Spacing = 2 };
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            Opacity = 0.65,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        sp.Children.Add(valueBlock);

        return new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 248, 250, 252)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 6, 8, 6),
            Child = sp,
        };
    }

    private async Task EmailShoppingListPdfAsync(IReadOnlyList<StockShoppingListRowViewModel> rows, ShoppingListPdfScope scope)
    {
        if (rows.Count == 0)
        {
            _vm.SetStatusMessage(scope == ShoppingListPdfScope.Merch
                ? "No merchandise to email."
                : "No bar & supplies to email.");
            return;
        }

        var isMerch = scope == ShoppingListPdfScope.Merch;
        var label = isMerch ? "merchandise" : "bar & supplies";
        var fileStem = isMerch ? "Merch_Shopping_List" : "Bar_Shopping_List";

        try
        {
            _vm.SetStatusMessage($"Building {label} PDF...");
            var bytes = ShoppingListPdfBuilder.Build(rows, scope);
            var fileName = $"{fileStem}_{DateTime.Now:yyyyMMdd}.pdf";
            var email = App.Services.GetRequiredService<IEmailSender>();

            _vm.SetStatusMessage("Sending email...");
            await email.SendAsync(
                subject: $"Nickeltown {label} shopping list — {DateTime.Now:dddd d MMMM yyyy}",
                body:
                    $"Attached is the {label} shopping list for items that need restocking."
                    + Environment.NewLine + Environment.NewLine
                    + "Sent automatically by Nickeltown POS v4.",
                attachments: new List<EmailAttachment>
                {
                    new(fileName, bytes, "application/pdf"),
                }).ConfigureAwait(true);
            _vm.SetStatusMessage($"{char.ToUpper(label[0])}{label[1..]} list emailed.");
        }
        catch (Exception ex)
        {
            _vm.SetStatusMessage($"Email failed: {ex.Message}");
        }
    }

    private static SolidColorBrush ParseAccentBrush(string accent)
    {
        if (accent.Length >= 9 && accent.StartsWith("#", StringComparison.Ordinal))
        {
            try
            {
                var hex = accent[1..];
                byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(a, r, g, b));
            }
            catch
            {
                // Fall through to default.
            }
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    private UIElement BuildReceiveStockShell()
    {
        StockEditorRow? selected = null;
        var searchBar = CreateEditableSearchBar("Search item name or barcode", out var searchBox);
        var resultsHost = new StackPanel { Spacing = 8 };
        var emptyHint = new TextBlock
        {
            Text = "Type a product name or scan a barcode to get started.",
            FontSize = 14,
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.72,
            Margin = new Thickness(4, 4, 4, 0),
        };

        var detailPanel = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed };
        var nameText = new TextBlock
        {
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
        };
        var currentHero = CreateWorkflowHeroValue("0");
        var packSizeRow = CreateIntegerRow("1", "Pack size", min: 1, max: 999999, buttonCaption: "#");
        var packsRow = CreateIntegerRow("1", "Packs bought", min: 1, max: 999999, buttonCaption: "#");
        var paidRow = CreateMoneyRow("0.00", "Total paid", buttonCaption: "$");
        var calcItems = new TextBlock { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        var calcEach = new TextBlock { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        var calcProfitPitstop = new TextBlock { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        var calcProfitBar = new TextBlock { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        var calcNew = new TextBlock { FontSize = 26, FontWeight = Microsoft.UI.Text.FontWeights.Bold };
        var saveBtn = WorkflowPrimaryButton("Save stock");
        saveBtn.IsEnabled = false;

        void RefreshCalcs()
        {
            if (selected is null)
            {
                return;
            }

            var packs = ParseInt(packsRow.TextBox.Text, 0);
            var packSize = ParseInt(packSizeRow.TextBox.Text, 0);
            var totalItems = packs * packSize;
            var paid = ParseMoney(paidRow.TextBox.Text);
            var pitstopSell = selected.PitstopPrice is > 0d ? (decimal)selected.PitstopPrice.Value : 0m;
            var barSell = selected.BarPrice is > 0d ? (decimal)selected.BarPrice.Value : 0m;
            var costEach = totalItems > 0 ? paid / totalItems : 0m;
            calcItems.Text = totalItems.ToString(CultureInfo.InvariantCulture);
            calcEach.Text = "$" + costEach.ToString("0.00", CultureInfo.InvariantCulture);
            calcProfitPitstop.Text = FormatProfitEach(pitstopSell, costEach);
            calcProfitBar.Text = FormatProfitEach(barSell, costEach);
            calcNew.Text = (selected.StockQty + totalItems).ToString(CultureInfo.InvariantCulture);
        }

        packsRow.TextBox.TextChanged += (_, _) => RefreshCalcs();
        packSizeRow.TextBox.TextChanged += (_, _) => RefreshCalcs();
        paidRow.TextBox.TextChanged += (_, _) => RefreshCalcs();

        void SelectItem(StockEditorRow found)
        {
            selected = found;
            nameText.Text = found.Name;
            currentHero.Text = found.StockQty.ToString(CultureInfo.InvariantCulture);
            packSizeRow.TextBox.Text = found.PurchaseUnitQty?.ToString(CultureInfo.InvariantCulture) ?? "1";
            packsRow.TextBox.Text = "1";
            paidRow.TextBox.Text = "0.00";
            detailPanel.Visibility = Visibility.Visible;
            emptyHint.Visibility = Visibility.Collapsed;
            resultsHost.Visibility = Visibility.Collapsed;
            saveBtn.IsEnabled = true;
            RefreshCalcs();
            _vm.SetStatusMessage("Enter packs bought and total paid.");
        }

        void RefreshSearchResults()
        {
            resultsHost.Children.Clear();
            var query = searchBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                emptyHint.Visibility = detailPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                resultsHost.Visibility = Visibility.Collapsed;
                return;
            }

            emptyHint.Visibility = Visibility.Collapsed;
            var matches = _vm.FindItemsBySearch(query, maxResults: 8);
            if (matches.Count == 0)
            {
                resultsHost.Children.Add(new TextBlock
                {
                    Text = "No matching items. Try a different name or barcode.",
                    FontSize = 14,
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Opacity = 0.75,
                    Margin = new Thickness(4, 4, 4, 0),
                });
                resultsHost.Visibility = Visibility.Visible;
                return;
            }

            if (matches.Count == 1)
            {
                var only = matches[0];
                var exact = string.Equals((only.Name ?? string.Empty).Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase)
                    || string.Equals((only.Sku ?? string.Empty).Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase);
                if (exact)
                {
                    SelectItem(only);
                    return;
                }
            }

            foreach (var match in matches)
            {
                var rowBtn = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Background = PosSurfaceBrush,
                    BorderBrush = PosBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 10, 12, 10),
                    MinHeight = 56,
                };
                var rowContent = new Grid { ColumnSpacing = 10 };
                rowContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var nameStack = new StackPanel { Spacing = 2 };
                nameStack.Children.Add(new TextBlock
                {
                    Text = match.Name,
                    FontSize = 15,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.WrapWholeWords,
                });
                if (!string.IsNullOrWhiteSpace(match.Sku))
                {
                    nameStack.Children.Add(new TextBlock
                    {
                        Text = match.Sku,
                        FontSize = 12,
                        Opacity = 0.65,
                    });
                }

                rowContent.Children.Add(nameStack);
                var stockBadge = new Border
                {
                    Padding = new Thickness(10, 6, 10, 6),
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 238, 242, 255)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = match.StockQty + " on hand",
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 30, 58, 138)),
                    },
                };
                Grid.SetColumn(stockBadge, 1);
                rowContent.Children.Add(stockBadge);
                rowBtn.Content = rowContent;
                var captured = match;
                rowBtn.Click += (_, _) => SelectItem(captured);
                resultsHost.Children.Add(rowBtn);
            }

            resultsHost.Visibility = Visibility.Visible;
        }

        searchBox.TextChanged += (_, _) => RefreshSearchResults();

        var changeItemBtn = WorkflowButton("Search again");
        changeItemBtn.Click += (_, _) =>
        {
            selected = null;
            detailPanel.Visibility = Visibility.Collapsed;
            saveBtn.IsEnabled = false;
            searchBox.Text = string.Empty;
            RefreshSearchResults();
        };

        var itemCard = CreateWorkflowCard(new StackPanel
        {
            Spacing = 8,
            Children =
            {
                nameText,
                CreateWorkflowSectionTitle("Current on hand"),
                currentHero,
                changeItemBtn,
            },
        });

        var purchaseGrid = new Grid { ColumnSpacing = 12, RowSpacing = 10 };
        purchaseGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        purchaseGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        purchaseGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        purchaseGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var packSizeField = Labeled("Pack size", packSizeRow);
        Grid.SetRow(packSizeField, 0);
        Grid.SetColumn(packSizeField, 0);
        purchaseGrid.Children.Add(packSizeField);
        var packsField = Labeled("Packs bought", packsRow);
        Grid.SetRow(packsField, 0);
        Grid.SetColumn(packsField, 1);
        purchaseGrid.Children.Add(packsField);
        var paidField = Labeled("Total paid", paidRow);
        Grid.SetRow(paidField, 1);
        Grid.SetColumn(paidField, 0);
        Grid.SetColumnSpan(paidField, 2);
        purchaseGrid.Children.Add(paidField);

        static StackPanel BuildCalcTile(string label, TextBlock value)
        {
            var tile = new StackPanel { Spacing = 4 };
            tile.Children.Add(CreateWorkflowSectionTitle(label));
            tile.Children.Add(value);
            return tile;
        }

        var itemsTile = BuildCalcTile("Items added", calcItems);
        var costTile = BuildCalcTile("Cost each", calcEach);
        var profitPitstopTile = BuildCalcTile("Profit Pitstop", calcProfitPitstop);
        var profitBarTile = BuildCalcTile("Profit Bar", calcProfitBar);
        var newStockTile = BuildCalcTile("New stock", calcNew);

        var summaryGrid = new Grid { ColumnSpacing = 12, RowSpacing = 10 };
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        summaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        summaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        summaryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(itemsTile, 0);
        Grid.SetColumn(itemsTile, 0);
        summaryGrid.Children.Add(itemsTile);
        Grid.SetRow(costTile, 0);
        Grid.SetColumn(costTile, 1);
        summaryGrid.Children.Add(costTile);
        Grid.SetRow(profitPitstopTile, 1);
        Grid.SetColumn(profitPitstopTile, 0);
        summaryGrid.Children.Add(profitPitstopTile);
        Grid.SetRow(profitBarTile, 1);
        Grid.SetColumn(profitBarTile, 1);
        summaryGrid.Children.Add(profitBarTile);
        Grid.SetRow(newStockTile, 2);
        Grid.SetColumn(newStockTile, 0);
        Grid.SetColumnSpan(newStockTile, 2);
        summaryGrid.Children.Add(newStockTile);

        detailPanel.Children.Add(itemCard);
        detailPanel.Children.Add(CreateWorkflowCard(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateWorkflowSectionTitle("Delivery details"),
                purchaseGrid,
            },
        }));
        detailPanel.Children.Add(CreateWorkflowCard(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                CreateWorkflowSectionTitle("Summary"),
                summaryGrid,
            },
        }));

        saveBtn.Click += async (_, _) =>
        {
            if (selected is null)
            {
                _vm.SetStatusMessage("Select an item first.");
                return;
            }

            if (await _vm.ReceiveStockAsync(
                    selected.ItemId,
                    ParseInt(packsRow.TextBox.Text, 0),
                    ParseInt(packSizeRow.TextBox.Text, 0),
                    ParseMoney(paidRow.TextBox.Text)).ConfigureAwait(true))
            {
                CloseFullScreen();
            }
        };

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(searchBar);
        body.Children.Add(emptyHint);
        body.Children.Add(resultsHost);
        body.Children.Add(detailPanel);

        var shell = new Grid { RowSpacing = 10 };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.Children.Add(CreateWorkflowHeader("Receive Stock", "Add stock after shopping"));
        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
        };
        Grid.SetRow(scroll, 1);
        shell.Children.Add(scroll);
        Grid.SetRow(saveBtn, 2);
        shell.Children.Add(saveBtn);
        return shell;
    }

    private UIElement BuildCountStockShell()
    {
        var queue = _vm.BuildStockCountQueue().ToList();
        var index = 0;
        var pending = new List<(long ItemId, int OldQty, int NewQty)>();
        var finished = false;
        var itemName = new TextBlock
        {
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            TextWrapping = TextWrapping.WrapWholeWords,
            TextAlignment = TextAlignment.Center,
        };
        var systemHero = CreateWorkflowHeroValue("0");
        Grid countStepper;
        TextBlock countValueLabel;
        var matchBadge = new Border
        {
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(999),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock
            {
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            },
        };
        var progressLabel = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = PosTextSecondaryBrush,
        };
        var progressTrack = new Border
        {
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = PosBorderBrush,
        };
        var progressFill = new Border
        {
            Height = 10,
            CornerRadius = new CornerRadius(5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 124, 58, 237)),
        };
        progressTrack.Child = progressFill;
        var wizardPanel = new StackPanel { Spacing = 14 };
        var summaryPanel = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };
        var nextBtn = WorkflowPrimaryButton("Next");
        var saveSummaryBtn = WorkflowPrimaryButton("Save stock count");
        saveSummaryBtn.Visibility = Visibility.Collapsed;

        void UpdateMatchBadge(int systemQty, int actualQty)
        {
            var delta = actualQty - systemQty;
            if (delta == 0)
            {
                matchBadge.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(32, 22, 163, 74));
                ((TextBlock)matchBadge.Child).Text = "Matches system";
                ((TextBlock)matchBadge.Child).Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 22, 163, 74));
                return;
            }

            var deltaText = delta > 0 ? "+" + delta : delta.ToString(CultureInfo.InvariantCulture);
            matchBadge.Background = delta > 0
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(32, 37, 99, 235))
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(32, 220, 38, 38));
            ((TextBlock)matchBadge.Child).Text = delta > 0 ? deltaText + " more than system" : deltaText + " vs system";
            ((TextBlock)matchBadge.Child).Foreground = delta > 0
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 37, 99, 235))
                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 38, 38));
        }

        void OnCountChanged(int actual)
        {
            if (index >= queue.Count || queue.Count == 0)
            {
                return;
            }

            UpdateMatchBadge(queue[index].StockQty, actual);
        }

        (countStepper, countValueLabel) = CreateLargeCountStepper(
            0,
            "Actual count",
            min: 0,
            max: 999999,
            onValueChanged: OnCountChanged);

        void UpdateProgressBar()
        {
            if (queue.Count == 0)
            {
                progressFill.Width = 0;
                progressLabel.Text = "No items to count";
                return;
            }

            var pct = Math.Clamp((double)(index + 1) / queue.Count, 0d, 1d);
            progressFill.Width = Math.Max(0, (progressTrack.ActualWidth > 0 ? progressTrack.ActualWidth : 320) * pct);
            progressLabel.Text = "Item " + Math.Min(index + 1, queue.Count) + " of " + queue.Count;
        }

        void ShowSummary()
        {
            finished = true;
            wizardPanel.Visibility = Visibility.Collapsed;
            summaryPanel.Visibility = Visibility.Visible;
            summaryPanel.Children.Clear();
            summaryPanel.Children.Add(new TextBlock
            {
                Text = "Review count changes",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            if (pending.Count == 0)
            {
                summaryPanel.Children.Add(new TextBlock
                {
                    Text = "No quantity changes — all counts matched the system.",
                    FontSize = 14,
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Opacity = 0.8,
                });
            }
            else
            {
                foreach (var c in pending)
                {
                    var label = queue.FirstOrDefault(r => r.ItemId == c.ItemId)?.Name ?? ("Item " + c.ItemId);
                    var delta = c.NewQty - c.OldQty;
                    var deltaText = delta > 0 ? "+" + delta : delta.ToString(CultureInfo.InvariantCulture);
                    var row = new Grid { ColumnSpacing = 10 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var nameText = new TextBlock
                    {
                        Text = label,
                        FontSize = 14,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextWrapping = TextWrapping.WrapWholeWords,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    row.Children.Add(nameText);
                    Grid.SetColumn(nameText, 0);
                    var qtyText = new TextBlock
                    {
                        Text = c.OldQty + " → " + c.NewQty,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = 0.85,
                    };
                    row.Children.Add(qtyText);
                    Grid.SetColumn(qtyText, 1);
                    var deltaBadge = new Border
                    {
                        Padding = new Thickness(8, 4, 8, 4),
                        CornerRadius = new CornerRadius(8),
                        Background = delta >= 0
                            ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(32, 22, 163, 74))
                            : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(32, 220, 38, 38)),
                        Child = new TextBlock
                        {
                            Text = deltaText,
                            FontSize = 12,
                            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                            Foreground = delta >= 0
                                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 22, 163, 74))
                                : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 38, 38)),
                        },
                    };
                    row.Children.Add(deltaBadge);
                    Grid.SetColumn(deltaBadge, 2);
                    summaryPanel.Children.Add(CreateWorkflowCard(row));
                }
            }

            nextBtn.Visibility = Visibility.Collapsed;
            saveSummaryBtn.Visibility = Visibility.Visible;
        }

        void ShowStep()
        {
            if (queue.Count == 0)
            {
                itemName.Text = "No items to count";
                systemHero.Text = "0";
                countValueLabel.Text = "0";
                nextBtn.IsEnabled = false;
                UpdateProgressBar();
                UpdateMatchBadge(0, 0);
                return;
            }

            if (index >= queue.Count)
            {
                ShowSummary();
                return;
            }

            var item = queue[index];
            itemName.Text = item.Name;
            systemHero.Text = item.StockQty.ToString(CultureInfo.InvariantCulture);
            countValueLabel.Text = item.StockQty.ToString(CultureInfo.InvariantCulture);
            UpdateMatchBadge(item.StockQty, item.StockQty);
            UpdateProgressBar();
        }

        progressTrack.SizeChanged += (_, _) => UpdateProgressBar();

        nextBtn.Click += (_, _) =>
        {
            if (finished || index >= queue.Count)
            {
                return;
            }

            var item = queue[index];
            var actual = ParseInt(countValueLabel.Text, item.StockQty);
            if (actual != item.StockQty)
            {
                pending.Add((item.ItemId, item.StockQty, actual));
            }

            index++;
            ShowStep();
        };

        saveSummaryBtn.Click += async (_, _) =>
        {
            var changes = pending.Select(p => (p.ItemId, p.NewQty)).ToList();
            var allIds = queue.Select(q => q.ItemId).ToList();
            if (await _vm.ApplyStockCountResultsAsync(changes, allIds).ConfigureAwait(true))
            {
                CloseFullScreen();
            }
        };

        Border CreateCompareTile(string title, UIElement content, bool emphasize = false)
        {
            var stack = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(CreateWorkflowSectionTitle(title));
            stack.Children.Add(content);
            return new Border
            {
                Padding = new Thickness(16, 14, 16, 14),
                CornerRadius = new CornerRadius(14),
                Background = emphasize
                    ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 245, 243, 255))
                    : new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 248, 250, 252)),
                BorderBrush = emphasize
                    ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 196, 181, 253))
                    : PosBorderBrush,
                BorderThickness = new Thickness(1),
                Child = stack,
            };
        }

        var compareGrid = new Grid { ColumnSpacing = 12 };
        compareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        compareGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var systemTile = CreateCompareTile("System says", systemHero);
        var countTile = CreateCompareTile("Your count", countStepper, emphasize: true);
        Grid.SetColumn(systemTile, 0);
        compareGrid.Children.Add(systemTile);
        Grid.SetColumn(countTile, 1);
        compareGrid.Children.Add(countTile);

        wizardPanel.Children.Add(CreateWorkflowCard(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                progressLabel,
                progressTrack,
            },
        }));
        wizardPanel.Children.Add(CreateWorkflowCard(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                itemName,
                compareGrid,
                matchBadge,
            },
        }));

        var host = new StackPanel { Spacing = 12 };
        host.Children.Add(wizardPanel);
        host.Children.Add(summaryPanel);

        var shell = new Grid { RowSpacing = 10 };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.Children.Add(CreateWorkflowHeader("Count Stock", "Check what is on the shelf"));
        var scroll = new ScrollViewer
        {
            Content = host,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
        };
        Grid.SetRow(scroll, 1);
        shell.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 8 };
        footer.Children.Add(nextBtn);
        footer.Children.Add(saveSummaryBtn);
        Grid.SetRow(footer, 2);
        shell.Children.Add(footer);
        ShowStep();
        return shell;
    }

    private UIElement BuildProductSetupShell()
    {
        var listHost = new StackPanel { Spacing = 6 };
        foreach (var row in _vm.PageRows)
        {
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = PosSurfaceBrush,
                BorderBrush = PosBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 10, 12, 10),
                MinHeight = 56,
            };
            var sp = new StackPanel { Spacing = 2 };
            sp.Children.Add(new TextBlock { Text = row.Name, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = row.CategoryLine, FontSize = 12, Opacity = 0.75 });
            btn.Content = sp;
            var captured = row;
            btn.Click += (_, _) =>
            {
                _vm.SelectRowCommand.Execute(captured);
                OpenAdvancedProductEditFromSetup();
            };
            listHost.Children.Add(btn);
        }

        var addBtn = WorkflowButton("Add product");
        addBtn.Click += async (_, _) => await OpenAddProductFromSetupAsync().ConfigureAwait(true);
        var shell = new Grid { RowSpacing = 8 };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.Children.Add(CreateWorkflowHeader("Product Setup", "Advanced pricing, barcodes, and catalog"));
        var hint = new TextBlock
        {
            Text = "Tap a product for advanced product setup (bar, pitstop, guest, specials, images, and more).",
            FontSize = 13,
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.75,
        };
        Grid.SetRow(hint, 1);
        shell.Children.Add(hint);
        var scroll = new ScrollViewer
        {
            Content = listHost,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
        };
        Grid.SetRow(scroll, 2);
        shell.Children.Add(scroll);
        Grid.SetRow(addBtn, 3);
        shell.Children.Add(addBtn);
        return shell;
    }

    private static Border CreateDivider() => new Border
    {
        Height = 1,
        Margin = new Thickness(0, 4, 0, 4),
        Background = PosBorderBrush,
    };

    private Grid WorkflowShellSingleAction(string title, string subtitle, UIElement body, Button action) =>
        WorkflowShellWithActions(title, subtitle, body, action);

    private Grid WorkflowShellWithActions(string title, string subtitle, UIElement body, FrameworkElement actions)
    {
        var shell = new Grid { RowSpacing = 10 };
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        shell.Children.Add(CreateWorkflowHeader(title, subtitle));
        var scroll = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
        };
        Grid.SetRow(scroll, 1);
        shell.Children.Add(scroll);
        Grid.SetRow(actions, 2);
        shell.Children.Add(actions);
        return shell;
    }

    private Button WorkflowButton(string label) => new Button
    {
        Content = label,
        MinHeight = 52,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Style = HeaderButtonStyle,
    };

    private Button WorkflowPrimaryButton(string label) => new Button
    {
        Content = label,
        MinHeight = 56,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        CornerRadius = new CornerRadius(14),
        Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 124, 58, 237)),
        Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
        FontSize = 16,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        BorderThickness = new Thickness(0),
    };

    private Grid CreateWorkflowHeader(string title, string? subtitle)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            stack.Children.Add(new TextBlock { Text = subtitle, FontSize = 13, Opacity = 0.72 });
        }

        grid.Children.Add(stack);
        var back = WorkflowButton("Back");
        back.Click += (_, _) => CloseFullScreen();
        Grid.SetColumn(back, 1);
        grid.Children.Add(back);
        return grid;
    }

    private static string FormatProfitEach(decimal sellPrice, decimal costEach) =>
        sellPrice > 0m
            ? "$" + (sellPrice - costEach).ToString("0.00", CultureInfo.InvariantCulture)
            : "—";

    private static int ParseInt(string? text, int fallback) =>
        int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static decimal ParseMoney(string? text) =>
        decimal.TryParse((text ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;
}
