using System.Globalization;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

public sealed partial class StockManagementPage
{
    private void StockHeroAdjust_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StockListRowViewModel row || !row.CanQuickAdjustStock)
        {
            return;
        }

        if (_vm.SelectRowCommand.CanExecute(row))
        {
            _vm.SelectRowCommand.Execute(row);
        }

        ShowQuickStockAdjustFlyout(row);
        _vm.SetStatusMessage("Tap + or - to adjust stock quickly.");
    }

    private void ShowQuickStockAdjustFlyout(StockListRowViewModel row)
    {
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock
        {
            Text = "Quick adjust stock",
            FontSize = 14,
            Opacity = 0.8,
        });

        body.Children.Add(BuildQuickStockDeltaRow(row, positive: true));
        body.Children.Add(BuildQuickStockDeltaRow(row, positive: false));

        ShowInPageStockModal(row.Name + " - " + row.StockHeroText, body, "Done", null, null, true, 520);
        _stockModalDimDismissEnabled = true;
    }

    private Grid BuildQuickStockDeltaRow(StockListRowViewModel row, bool positive)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        for (var i = 0; i < 3; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var amounts = new[] { 1, 6, 24 };
        for (var i = 0; i < amounts.Length; i++)
        {
            var amount = amounts[i];
            var delta = positive ? amount : -amount;
            var label = (positive ? "+" : "-") + amount.ToString(CultureInfo.InvariantCulture);
            var btn = new Button
            {
                Content = label,
                MinHeight = 52,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
            };
            btn.Click += async (_, _) => await ApplyQuickStockFromFlyoutAsync(row, delta).ConfigureAwait(true);
            Grid.SetColumn(btn, i);
            grid.Children.Add(btn);
        }

        return grid;
    }

    private async Task ApplyQuickStockFromFlyoutAsync(StockListRowViewModel row, int delta)
    {
        if (_vm.SelectRowCommand.CanExecute(row))
        {
            _vm.SelectRowCommand.Execute(row);
        }

        if (await _vm.ApplyQuickStockDeltaAndPersistCoreAsync(delta).ConfigureAwait(true))
        {
            DismissInPageStockModal();
            await _vm.LoadAsync(resetStatusMessage: false).ConfigureAwait(true);
            _vm.SetStatusMessage(delta > 0 ? $"Added {delta} to stock." : $"Removed {-delta} from stock.");
        }
    }
}