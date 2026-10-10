using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace NickeltownPOSV4.Controls;

public sealed partial class CatalogPagePager : UserControl
{
    public static readonly DependencyProperty PreviousCommandProperty = DependencyProperty.Register(
        nameof(PreviousCommand),
        typeof(ICommand),
        typeof(CatalogPagePager),
        new PropertyMetadata(null));

    public static readonly DependencyProperty NextCommandProperty = DependencyProperty.Register(
        nameof(NextCommand),
        typeof(ICommand),
        typeof(CatalogPagePager),
        new PropertyMetadata(null));

    public static readonly DependencyProperty PageLabelProperty = DependencyProperty.Register(
        nameof(PageLabel),
        typeof(string),
        typeof(CatalogPagePager),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowPagerProperty = DependencyProperty.Register(
        nameof(ShowPager),
        typeof(bool),
        typeof(CatalogPagePager),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsPreviousEnabledProperty = DependencyProperty.Register(
        nameof(IsPreviousEnabled),
        typeof(bool),
        typeof(CatalogPagePager),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsNextEnabledProperty = DependencyProperty.Register(
        nameof(IsNextEnabled),
        typeof(bool),
        typeof(CatalogPagePager),
        new PropertyMetadata(true));

    public static readonly DependencyProperty UseDarkChromeProperty = DependencyProperty.Register(
        nameof(UseDarkChrome),
        typeof(bool),
        typeof(CatalogPagePager),
        new PropertyMetadata(false, OnUseDarkChromeChanged));

    public CatalogPagePager()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyChrome();
    }

    private static void OnUseDarkChromeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CatalogPagePager pager)
        {
            pager.ApplyChrome();
        }
    }

    private void ApplyChrome()
    {
        if (PagerChrome is null || PageLabelText is null || PreviousButton is null || NextButton is null)
        {
            return;
        }

        if (UseDarkChrome)
        {
            PagerChrome.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1B, 0x1F, 0x25));
            PagerChrome.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xC4, 0xCA, 0xD2));
            PageLabelText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5));
            PagerChrome.MinHeight = 52;
            PagerChrome.Padding = new Thickness(8, 4, 8, 4);

            if (Resources.TryGetValue("BathurstPagerNavButtonStyle", out var bathurstStyle) &&
                bathurstStyle is Style style)
            {
                PreviousButton.Style = style;
                NextButton.Style = style;
            }
        }
        else
        {
            PagerChrome.MinHeight = 40;
            PagerChrome.Padding = new Thickness(4, 2, 4, 2);

            if (Application.Current?.Resources.TryGetValue("TabsPagerPillButtonStyle", out var pill) == true &&
                pill is Style pillStyle)
            {
                PreviousButton.Style = pillStyle;
                NextButton.Style = pillStyle;
            }

            if (Application.Current?.Resources.TryGetValue("PosSurfaceBrush", out var surface) == true &&
                surface is Brush surfaceBrush &&
                Application.Current.Resources.TryGetValue("PosBorderBrush", out var border) == true &&
                border is Brush borderBrush &&
                Application.Current.Resources.TryGetValue("PosTextPrimaryBrush", out var text) == true &&
                text is Brush textBrush)
            {
                PagerChrome.Background = surfaceBrush;
                PagerChrome.BorderBrush = borderBrush;
                PageLabelText.Foreground = textBrush;
            }
        }
    }

    public ICommand? PreviousCommand
    {
        get => (ICommand?)GetValue(PreviousCommandProperty);
        set => SetValue(PreviousCommandProperty, value);
    }

    public ICommand? NextCommand
    {
        get => (ICommand?)GetValue(NextCommandProperty);
        set => SetValue(NextCommandProperty, value);
    }

    public string PageLabel
    {
        get => (string)GetValue(PageLabelProperty);
        set => SetValue(PageLabelProperty, value);
    }

    public bool ShowPager
    {
        get => (bool)GetValue(ShowPagerProperty);
        set => SetValue(ShowPagerProperty, value);
    }

    public bool IsPreviousEnabled
    {
        get => (bool)GetValue(IsPreviousEnabledProperty);
        set => SetValue(IsPreviousEnabledProperty, value);
    }

    public bool IsNextEnabled
    {
        get => (bool)GetValue(IsNextEnabledProperty);
        set => SetValue(IsNextEnabledProperty, value);
    }

    /// <summary>When true, uses Bathurst dark charcoal chrome with explicit nav visual states.</summary>
    public bool UseDarkChrome
    {
        get => (bool)GetValue(UseDarkChromeProperty);
        set => SetValue(UseDarkChromeProperty, value);
    }
}
