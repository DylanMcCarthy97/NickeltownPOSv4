using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Controls;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Stock;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NickeltownPOSV4.Views;

public sealed partial class StockManagementPage
{
    private static readonly string[] StockImageUploadFileExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".bmp",
    ];

    internal async Task<string?> PickSourceImagePathAsync()
    {
        var hwnd = _windowHandleProvider.WindowHandle;
        if (hwnd == 0)
        {
            return null;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        foreach (var ext in StockImageUploadFileExtensions)
        {
            picker.FileTypeFilter.Add(ext);
        }

        InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    internal async Task<bool> PickAndStoreProductImageAsync(bool persistImmediately = true)
    {
        var source = await PickSourceImagePathAsync().ConfigureAwait(true);
        if (source is null)
        {
            return false;
        }

        var ok = await _vm.ApplyProductImageFromPickerAsync(source, persistImmediately).ConfigureAwait(true);
        if (ok)
        {
            App.Services.GetRequiredService<PosCatalogAutoRefreshService>().PulseRefresh();
        }

        return ok;
    }

    internal async Task<bool> ShowProductImagePickerDialogAsync(bool persistOnPick, Action? onChanged = null)
    {
        if (XamlRoot is null)
        {
            return false;
        }

        var content = new StackPanel { Spacing = 14, MaxWidth = 520 };
        content.Children.Add(new TextBlock
        {
            Text = "Choose a stock icon or upload your own photo.",
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = (Brush)Application.Current.Resources["PosTextSecondaryBrush"],
        });

        var iconGrid = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        const int columns = 4;
        var icons = StockProductIconCatalog.All;
        var rows = (icons.Count + columns - 1) / columns;
        for (var r = 0; r < rows; r++)
        {
            iconGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var c = 0; c < columns; c++)
        {
            iconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        string? selectedIconId = null;
        if (StockProductIconCatalog.TryParseStoragePath(_vm.DetailImagePath, out var currentId))
        {
            selectedIconId = currentId;
        }

        var dialog = new ContentDialog
        {
            Title = "Product image",
            Content = content,
            CloseButtonText = "Done",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        for (var i = 0; i < icons.Count; i++)
        {
            var icon = icons[i];
            var row = i / columns;
            var col = i % columns;
            var tile = new Button
            {
                MinWidth = 108,
                MinHeight = 88,
                Padding = new Thickness(6, 8, 6, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                Background = (Brush)Application.Current.Resources["PosSurfaceBrush"],
                BorderBrush = string.Equals(selectedIconId, icon.Id, StringComparison.OrdinalIgnoreCase)
                    ? (Brush)Application.Current.Resources["PosAccentBrush"]
                    : (Brush)Application.Current.Resources["PosBorderBrush"],
            };
            var label = new StackPanel
            {
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            label.Children.Add(new TextBlock
            {
                Text = icon.Emoji,
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            });
            label.Children.Add(new TextBlock
            {
                Text = icon.Label,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.WrapWholeWords,
                MaxLines = 2,
            });
            tile.Content = label;
            tile.Click += async (_, _) =>
            {
                if (await _vm.ApplyStockProductIconAsync(icon.Id, persistOnPick).ConfigureAwait(true))
                {
                    App.Services.GetRequiredService<PosCatalogAutoRefreshService>().PulseRefresh();
                    onChanged?.Invoke();
                    dialog.Hide();
                }
            };
            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, col);
            iconGrid.Children.Add(tile);
        }

        content.Children.Add(iconGrid);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var uploadBtn = new Button
        {
            Content = "Upload photo",
            MinHeight = 48,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        uploadBtn.Click += async (_, _) =>
        {
            if (await PickAndStoreProductImageAsync(persistOnPick).ConfigureAwait(true))
            {
                onChanged?.Invoke();
                dialog.Hide();
            }
        };
        actions.Children.Add(uploadBtn);

        var clearBtn = new Button
        {
            Content = "Use category default",
            MinHeight = 48,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        clearBtn.Click += async (_, _) =>
        {
            if (await _vm.ClearProductImageAsync(persistOnPick).ConfigureAwait(true))
            {
                App.Services.GetRequiredService<PosCatalogAutoRefreshService>().PulseRefresh();
                onChanged?.Invoke();
                dialog.Hide();
            }
        };
        actions.Children.Add(clearBtn);
        content.Children.Add(actions);

        PosContentDialogHelper.ApplyPosStyle(dialog);
        _ = await dialog.ShowAsync();
        return true;
    }

    internal StackPanel CreateProductImagePickerBlock(bool persistOnPick, Action? onChanged = null)
    {
        var previewHost = new Border
        {
            Width = 120,
            Height = 120,
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["PosCanvasBrush"],
            BorderBrush = (Brush)Application.Current.Resources["PosBorderBrush"],
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var preview = new CatalogProductImage
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        previewHost.Child = preview;

        void SyncPreview()
        {
            preview.ImagePath = StockProductIconCatalog.IsStoragePath(_vm.DetailImagePath)
                ? _vm.DetailImagePath
                : _vm.DetailImagePreviewPath;
            preview.FallbackEmoji = _vm.DetailImagePreviewEmoji;
        }

        SyncPreview();

        var changeBtn = new Button
        {
            Content = "Change image",
            MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = (Style)Application.Current.Resources["TabsHeaderActionButtonStyle"],
        };
        changeBtn.Click += async (_, _) =>
        {
            await ShowProductImagePickerDialogAsync(persistOnPick, () =>
            {
                SyncPreview();
                onChanged?.Invoke();
            }).ConfigureAwait(true);
            SyncPreview();
            onChanged?.Invoke();
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(previewHost);
        panel.Children.Add(changeBtn);
        return panel;
    }
}
