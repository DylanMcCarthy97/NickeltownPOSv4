using System.Globalization;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Views;

/// <summary>Catalog health checklist full-screen (data-quality issues before race day).</summary>
public sealed partial class StockManagementPage
{
    internal void OpenCatalogHealthFullScreen() =>
        ShowFullScreen(StockManagementScreen.CatalogHealth, BuildCatalogHealthShell());

    private UIElement BuildCatalogHealthShell()
    {
        var issues = _vm.BuildCatalogHealthIssues();
        var summary = StockCatalogHealthService.Summarize(issues);

        var root = new Grid { RowSpacing = 10 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { ColumnSpacing = 12 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = "Catalog health",
                    FontSize = 22,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                },
                new TextBlock
                {
                    Text = "Shared items can use Bar price in Pitstop. Merch barcodes are optional. Fix issues without leaving this screen.",
                    FontSize = 13,
                    Opacity = 0.8,
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            },
        });
        var close = new Button
        {
            Content = "Back",
            MinHeight = 44,
            MinWidth = 96,
            Style = HeaderButtonStyle,
        };
        close.Click += (_, _) =>
        {
            _resumeCatalogHealthAfterQuickEdit = false;
            CloseFullScreenToHome();
        };
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var summaryPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        summaryPanel.Children.Add(BuildHealthChip(
            issues.Count == 0 ? "All clear" : issues.Count.ToString(CultureInfo.InvariantCulture) + " issues",
            issues.Count == 0));
        foreach (var s in summary.Take(6))
        {
            summaryPanel.Children.Add(BuildHealthChip(
                s.Kind + " · " + s.Count.ToString(CultureInfo.InvariantCulture),
                ok: false));
        }

        var summaryScroll = new ScrollViewer
        {
            Content = summaryPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Disabled,
        };
        Grid.SetRow(summaryScroll, 1);
        root.Children.Add(summaryScroll);

        if (issues.Count == 0)
        {
            var empty = new Border
            {
                Style = PosCardBorderStyle,
                Padding = new Thickness(16),
                Child = new TextBlock
                {
                    Text = "No catalog issues found. Prices, barcodes, and stock modes look consistent.",
                    FontSize = 15,
                    TextWrapping = TextWrapping.WrapWholeWords,
                },
            };
            Grid.SetRow(empty, 2);
            root.Children.Add(empty);
        }
        else
        {
            var list = new ListView
            {
                ItemsSource = issues,
                IsItemClickEnabled = true,
                SelectionMode = ListViewSelectionMode.None,
            };
            list.ItemTemplate = BuildHealthIssueTemplate();
            list.ItemClick += async (_, e) =>
            {
                if (e.ClickedItem is not StockCatalogHealthIssue issue)
                {
                    return;
                }

                var row = _vm.PageRows.FirstOrDefault(r => r.ItemId == issue.ItemId)
                    ?? _vm.FindListRowByItemId(issue.ItemId);
                if (row is null)
                {
                    _vm.SetStatusMessage("Item not found — refresh stock and try again.");
                    return;
                }

                // Keep Catalog Health open under the quick-edit modal; refresh list after save.
                _resumeCatalogHealthAfterQuickEdit = true;
                await OpenQuickEditAsync(row, resumeCatalogHealth: true).ConfigureAwait(true);
            };
            Grid.SetRow(list, 2);
            root.Children.Add(list);
        }

        var footer = new TextBlock
        {
            Text = "Tap an issue to quick-edit. After Save you stay on Catalog health with an updated list.",
            FontSize = 12,
            Opacity = 0.75,
        };
        Grid.SetRow(footer, 3);
        root.Children.Add(footer);
        return root;
    }

    private static Border BuildHealthChip(string text, bool ok)
    {
        var accent = ok ? ColorHelper.FromArgb(0xFF, 0x16, 0xA3, 0x4A) : ColorHelper.FromArgb(0xFF, 0xDC, 0x26, 0x26);
        var fill = ok ? ColorHelper.FromArgb(0x22, 0x16, 0xA3, 0x4A) : ColorHelper.FromArgb(0x22, 0xDC, 0x26, 0x26);
        return new Border
        {
            Padding = new Thickness(10, 6, 10, 6),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(fill),
            BorderBrush = new SolidColorBrush(accent),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            },
        };
    }

    private static DataTemplate BuildHealthIssueTemplate()
    {
        // Built in code so we do not need a new XAML resource dictionary for this screen.
        var xaml =
            """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Border Padding="12,10" Margin="0,0,0,8" CornerRadius="12"
                      BorderThickness="1" BorderBrush="{StaticResource PosBorderBrush}"
                      Background="{StaticResource PosSurfaceBrush}">
                <StackPanel Spacing="2">
                  <TextBlock Text="{Binding ItemName}" FontSize="15" FontWeight="SemiBold"
                             Foreground="{StaticResource PosTextPrimaryBrush}" TextTrimming="CharacterEllipsis" />
                  <TextBlock Text="{Binding Kind}" FontSize="12" FontWeight="SemiBold"
                             Foreground="{StaticResource PosAccentBrush}" />
                  <TextBlock Text="{Binding Detail}" FontSize="12" TextWrapping="WrapWholeWords"
                             Foreground="{StaticResource PosTextSecondaryBrush}" />
                </StackPanel>
              </Border>
            </DataTemplate>
            """;
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }
}
