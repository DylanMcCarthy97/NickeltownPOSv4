using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

    public CatalogPagePager()
    {
        InitializeComponent();
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
}