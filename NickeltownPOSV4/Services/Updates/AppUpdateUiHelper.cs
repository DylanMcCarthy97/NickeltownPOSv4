using System;
using NickeltownPOSV4.Services;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace NickeltownPOSV4.Services.Updates;

public static class AppUpdateUiHelper
{
    public static async Task<bool> TryHandleStartupUpdateAsync(XamlRoot xamlRoot)
    {
        var configService = App.Services.GetRequiredService<Settings.IAppUpdateConfigService>();
        var cfg = await configService.LoadAsync().ConfigureAwait(true);
        if (!cfg.CheckOnStartup)
        {
            return false;
        }

        return await TryPromptAndInstallAsync(xamlRoot, autoInstall: cfg.AutoInstall).ConfigureAwait(true);
    }

    public static async Task<bool> TryPromptAndInstallAsync(XamlRoot xamlRoot, bool autoInstall = false)
    {
        var updates = App.Services.GetRequiredService<IAppUpdateService>();
        var check = await updates.CheckForUpdateAsync().ConfigureAwait(true);
        if (!check.UpdateAvailable || check.Manifest is null)
        {
            return false;
        }

        var manifest = check.Manifest;
        var notes = string.IsNullOrWhiteSpace(manifest.ReleaseNotes)
            ? string.Empty
            : manifest.ReleaseNotes.Trim() + "\n\n";

        if (!autoInstall)
        {
            var dlg = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Update available",
                Content = $"{notes}Version {manifest.Version} is ready (you have {AppVersionInfo.CurrentVersionString}).\n\nInstall now? The app will restart.",
                PrimaryButtonText = "Install now",
                CloseButtonText = manifest.Mandatory ? string.Empty : "Later",
                DefaultButton = ContentDialogButton.Primary,
            };

            if (manifest.Mandatory)
            {
                dlg.CloseButtonText = string.Empty;
            }

            PosContentDialogHelper.ApplyPosStyle(dlg);
            var choice = await dlg.ShowAsync();
            if (choice != ContentDialogResult.Primary)
            {
                return false;
            }
        }

        var progressDlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Installing update",
            Content = new ProgressRing { IsActive = true, Width = 48, Height = 48 },
            IsPrimaryButtonEnabled = false,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = string.Empty,
        };

        var progressText = new TextBlock { Text = "Downloading...", HorizontalAlignment = HorizontalAlignment.Center };
        if (progressDlg.Content is ProgressRing ring)
        {
            progressDlg.Content = new StackPanel
            {
                Spacing = 12,
                Children = { ring, progressText },
            };
        }

        PosContentDialogHelper.ApplyPosStyle(progressDlg);
        _ = progressDlg.ShowAsync();

        var progress = new Progress<string>(msg =>
            TcxLayoutDiagnostics.TryEnqueueNormal(() => progressText.Text = msg));

        var install = await updates.InstallUpdateAsync(manifest, progress).ConfigureAwait(true);
        progressDlg.Hide();

        if (!install.Ok)
        {
            var err = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Update failed",
                Content = install.ErrorMessage ?? "Could not install the update.",
                CloseButtonText = "OK",
            };
            PosContentDialogHelper.ApplyPosStyle(err);
            await err.ShowAsync();
            return false;
        }

        if (install.AppShutdownRequested)
        {
            Application.Current.Exit();
        }

        return true;
    }
}
