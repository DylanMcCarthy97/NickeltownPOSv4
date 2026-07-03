using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace NickeltownPOSV4.Services;

public static class PosContentDialogHelper
{
    public static void ApplyPosStyle(ContentDialog dialog)
    {
        dialog.CornerRadius = new CornerRadius(14);
        if (Application.Current?.Resources.TryGetValue("PosSurfaceBrush", out var bg) == true && bg is Brush background)
        {
            dialog.Background = background;
        }
    }

    public static ContentDialog Create(
        XamlRoot xamlRoot,
        string title,
        object content,
        string? primaryButtonText = null,
        string? closeButtonText = "Cancel",
        ContentDialogButton defaultButton = ContentDialogButton.Close)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = defaultButton,
        };
        ApplyPosStyle(dialog);
        return dialog;
    }
}