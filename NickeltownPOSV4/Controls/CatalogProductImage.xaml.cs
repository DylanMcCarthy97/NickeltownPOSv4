using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.Controls;

public sealed partial class CatalogProductImage : UserControl
{
    public static readonly DependencyProperty ImagePathProperty = DependencyProperty.Register(
        nameof(ImagePath),
        typeof(string),
        typeof(CatalogProductImage),
        new PropertyMetadata(null, OnImagePathChanged));

    public static readonly DependencyProperty FallbackEmojiProperty = DependencyProperty.Register(
        nameof(FallbackEmoji),
        typeof(string),
        typeof(CatalogProductImage),
        new PropertyMetadata(string.Empty, OnImagePathChanged));

    public CatalogProductImage()
    {
        InitializeComponent();
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    public string FallbackEmoji
    {
        get => (string)GetValue(FallbackEmojiProperty);
        set => SetValue(FallbackEmojiProperty, value);
    }

    private static void OnImagePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CatalogProductImage c)
        {
            c.ApplyPath(c.ImagePath);
        }
    }

    private void ApplyPath(string? path)
    {
        PlaceholderIcon.Visibility = Visibility.Collapsed;
        FallbackEmojiText.Visibility = Visibility.Collapsed;
        Photo.Visibility = Visibility.Visible;
        Photo.Source = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            ShowEmojiFallback();
            return;
        }

        if (StockProductIconCatalog.TryGetFromStoragePath(path, out var stockIcon))
        {
            ShowStockIcon(stockIcon);
            return;
        }

        var s = path.Trim();
        try
        {
            if (Uri.TryCreate(s, UriKind.Absolute, out var absolute))
            {
                Photo.Source = new BitmapImage(absolute) { DecodePixelWidth = 96 };
                return;
            }

            var full = Path.GetFullPath(s);
            if (!File.Exists(full))
            {
                ShowEmojiFallback();
                return;
            }

            Photo.Source = new BitmapImage(new Uri(full)) { DecodePixelWidth = 128 };
        }
        catch (UriFormatException)
        {
            ShowEmojiFallback();
        }
        catch (IOException)
        {
            ShowEmojiFallback();
        }
        catch (UnauthorizedAccessException)
        {
            ShowEmojiFallback();
        }
    }

    private void Photo_ImageFailed(object sender, ExceptionRoutedEventArgs e) => ShowEmojiFallback();

    private void ShowStockIcon(StockProductIconCatalog.StockProductIcon stockIcon)
    {
        if (stockIcon.HasPackagedSvg)
        {
            try
            {
                Photo.Source = new SvgImageSource(new Uri(stockIcon.PackagedSvgUri!));
                Photo.Visibility = Visibility.Visible;
                FallbackEmojiText.Visibility = Visibility.Collapsed;
                return;
            }
            catch (UriFormatException)
            {
            }
        }

        Photo.Source = null;
        Photo.Visibility = Visibility.Collapsed;
        FallbackEmojiText.Text = stockIcon.Emoji;
        FallbackEmojiText.Visibility = Visibility.Visible;
    }

    private void ShowEmojiFallback()
    {
        Photo.Source = null;
        Photo.Visibility = Visibility.Collapsed;
        var emoji = string.IsNullOrWhiteSpace(FallbackEmoji)
            ? StockItemImageResolver.GetCategoryFallbackEmoji(null)
            : FallbackEmoji;
        FallbackEmojiText.Text = emoji;
        FallbackEmojiText.Visibility = Visibility.Visible;
        PlaceholderIcon.Visibility = Visibility.Collapsed;
    }
}
