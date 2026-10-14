using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

/// <summary>Volunteer quick-edit modal: qty, warn/target, mode, prices, active — without full item wizard.</summary>
public sealed partial class StockManagementPage
{
    internal async Task OpenQuickEditAsync(StockListRowViewModel row)
    {
        if (_vm.SelectRowCommand.CanExecute(row))
        {
            _vm.SelectRowCommand.Execute(row);
        }

        if (!_vm.HasSelection)
        {
            return;
        }

        var stockQtyRow = CreateEditIntegerRow(_vm.DetailStockText, "Current stock", 0, 999999);
        var warnRow = CreateEditIntegerRow(_vm.DetailWarnMeBelowText, "Warn Me Below", 0, 999999);
        var targetRow = CreateEditIntegerRow(_vm.DetailPreferredStockLevelText, "Target Amount", 0, 999999);
        var modeBox = CreateStockModePicker(_vm.DetailStockMode);
        var barRow = CreateEditMoneyRow(_vm.BarPriceText, "Bar price");
        var pitRow = CreateEditMoneyRow(_vm.PitstopPriceText, "Pitstop price");
        var activeSwitch = new ToggleSwitch
        {
            Header = "Active in catalog",
            IsOn = _vm.DetailIsActive,
            OnContent = "Active",
            OffContent = "Inactive",
        };

        var qtyGrid = new Grid { ColumnSpacing = 10 };
        qtyGrid.ColumnDefinitions.Add(new ColumnDefinition());
        qtyGrid.ColumnDefinitions.Add(new ColumnDefinition());
        qtyGrid.ColumnDefinitions.Add(new ColumnDefinition());
        var onHand = Labeled("On hand", stockQtyRow);
        var warn = Labeled("Warn below", warnRow);
        var target = Labeled("Target", targetRow);
        Grid.SetColumn(onHand, 0);
        Grid.SetColumn(warn, 1);
        Grid.SetColumn(target, 2);
        qtyGrid.Children.Add(onHand);
        qtyGrid.Children.Add(warn);
        qtyGrid.Children.Add(target);

        var priceGrid = new Grid { ColumnSpacing = 10 };
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition());
        priceGrid.ColumnDefinitions.Add(new ColumnDefinition());
        var bar = Labeled("Bar price", barRow);
        var pit = Labeled("Pitstop price", pitRow);
        Grid.SetColumn(bar, 0);
        Grid.SetColumn(pit, 1);
        priceGrid.Children.Add(bar);
        priceGrid.Children.Add(pit);

        var body = new StackPanel { Spacing = 12, MaxWidth = 720 };
        body.Children.Add(new TextBlock
        {
            Text = row.CategoryLine,
            FontSize = 13,
            Opacity = 0.75,
        });
        body.Children.Add(new TextBlock
        {
            Text = "Quick edit — sales deduct stock when mode is Tracked or Sell until gone (including Food).",
            FontSize = 12,
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.85,
        });
        body.Children.Add(Labeled("How we stock this item", modeBox));
        body.Children.Add(qtyGrid);
        body.Children.Add(priceGrid);
        body.Children.Add(activeSwitch);

        var fullEditBtn = new Button
        {
            Content = "Full edit…",
            MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        fullEditBtn.Click += async (_, _) =>
        {
            DismissInPageStockModal();
            _resumeProductSetupAfterItemEdit = false;
            await OpenItemEditFullScreenAsync().ConfigureAwait(true);
        };
        body.Children.Add(fullEditBtn);

        ShowInPageStockModal(
            row.Name,
            body,
            "Cancel",
            "Save",
            async () =>
            {
                _vm.DetailStockText = stockQtyRow.TextBox.Text ?? string.Empty;
                var warnText = warnRow.TextBox.Text ?? string.Empty;
                _vm.DetailWarnMeBelowText = warnText;
                _vm.LowStockThresholdText = warnText;
                var targetText = targetRow.TextBox.Text ?? string.Empty;
                _vm.DetailPreferredStockLevelText = targetText;
                _vm.DetailParLevelText = targetText;
                _vm.DetailStockMode = modeBox.SelectedItem is ComboBoxItem { Tag: string mode }
                    ? mode
                    : modeBox.SelectedItem as string ?? _vm.DetailStockMode;
                _vm.SyncDetailFlagsFromStockMode();
                _vm.BarPriceText = barRow.TextBox.Text ?? string.Empty;
                _vm.PitstopPriceText = pitRow.TextBox.Text ?? string.Empty;
                _vm.DetailIsActive = activeSwitch.IsOn;

                if (await _vm.TryPersistCurrentItemAsync().ConfigureAwait(true))
                {
                    DismissInPageStockModal();
                    await _vm.LoadAsync(resetStatusMessage: false).ConfigureAwait(true);
                    _vm.SetStatusMessage("Saved " + row.Name + ".");
                }
            },
            dismissOnDimTap: false,
            modalMaxWidth: 760);
    }
}
