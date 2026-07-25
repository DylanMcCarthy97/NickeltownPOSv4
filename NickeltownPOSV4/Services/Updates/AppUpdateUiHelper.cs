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

        if (!autoInstall)
        {
            var prompt = AppUpdateDialogFactory.CreateAvailableDialog(xamlRoot, manifest);
            var choice = await prompt.ShowAsync();
            if (choice != ContentDialogResult.Primary)
            {
                return false;
            }
        }

        var installDialog = AppUpdateDialogFactory.CreateInstallDialog(xamlRoot, manifest.Version);
        _ = installDialog.Dialog.ShowAsync();

        var progress = new Progress<AppUpdateProgress>(update =>
            TcxLayoutDiagnostics.TryEnqueueNormal(() => installDialog.Report(update)));

        var install = await updates.InstallUpdateAsync(manifest, progress).ConfigureAwait(true);
        installDialog.Dialog.Hide();

        if (!install.Ok)
        {
            await AppUpdateDialogFactory.CreateFailedDialog(xamlRoot, install.ErrorMessage).ShowAsync();
            return false;
        }

        if (install.AppShutdownRequested)
        {
            AppUpdateRestartHelper.FinishUpdateAndExit(manifest.Version);
        }

        return true;
    }
}
