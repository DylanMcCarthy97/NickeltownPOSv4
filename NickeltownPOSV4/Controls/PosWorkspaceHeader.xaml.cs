using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NickeltownPOSV4.Controls;

public sealed partial class PosWorkspaceHeader : UserControl
{
    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(PosWorkspaceHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RightContentProperty =
        DependencyProperty.Register(nameof(RightContent), typeof(object), typeof(PosWorkspaceHeader), new PropertyMetadata(null));

    public PosWorkspaceHeader()
    {
        InitializeComponent();
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }
}