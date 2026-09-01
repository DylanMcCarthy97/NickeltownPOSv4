using System;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsUndoConfirmDialog
{
    public const string Title = "Undo Last Transaction";

    public static async Task<bool> ShowAsync(XamlRoot? xamlRoot, TabUndoPreview preview)
    {
        if (xamlRoot is null)
        {
            return false;
        }

        var content = new StackPanel
        {
            Spacing = 6,
            MinWidth = 260,
        };

        content.Children.Add(new TextBlock
        {
            Text = preview.Headline,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = ThemeBrush("PosTextPrimaryBrush"),
        });

        if (!string.IsNullOrWhiteSpace(preview.AmountText))
        {
            content.Children.Add(new TextBlock
            {
                Text = preview.AmountText,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeBrush("PosTextPrimaryBrush"),
            });
        }

        if (!string.IsNullOrWhiteSpace(preview.DetailLine))
        {
            content.Children.Add(new TextBlock
            {
                Text = preview.DetailLine,
                FontSize = 14,
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = ThemeBrush("PosTextSecondaryBrush"),
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = preview.TimeText,
            FontSize = 14,
            Foreground = ThemeBrush("PosTextSecondaryBrush"),
        });

        var dlg = PosContentDialogHelper.Create(
            xamlRoot,
            Title,
            content,
            primaryButtonText: "Undo",
            closeButtonText: "Cancel",
            defaultButton: ContentDialogButton.Close);

        var result = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        return result == ContentDialogResult.Primary;
    }

    private static Brush? ThemeBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Brush brush)
        {
            return brush;
        }

        return null;
    }
}
