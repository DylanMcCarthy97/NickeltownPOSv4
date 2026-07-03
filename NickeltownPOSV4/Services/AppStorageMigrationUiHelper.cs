using System;
using NickeltownPOSV4.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NickeltownPOSV4.Services;

/// <summary>Offers to migrate legacy POS data into Documents\NickeltownPOS on first startup.</summary>
public static class AppStorageMigrationUiHelper
{
    public static async Task RunStartupMigrationsAsync(XamlRoot xamlRoot)
    {
        var paths = App.Services.GetRequiredService<IAppStoragePaths>();
        paths.EnsureDirectories();

        var migration = App.Services.GetRequiredService<IAppStorageMigrationService>();
        var window = App.Services.GetRequiredService<IWindowHandleProvider>();

        await OfferDatabaseMigrationAsync(xamlRoot, paths, migration).ConfigureAwait(true);
        await OfferSquareConfigMigrationAsync(xamlRoot, migration).ConfigureAwait(true);
        await OfferProductImagesMigrationAsync(xamlRoot, paths, migration, window).ConfigureAwait(true);

        if (paths.IsMsixPackaged && paths.HasWritableDataBesideExecutable())
        {
            await ShowInfoAsync(
                xamlRoot,
                "Data beside the app install folder",
                "This POS is running as an MSIX package. Important data (database, Square config, images) must live under Documents\\NickeltownPOS, not beside the installed app. "
                    + "Files were detected next to the app - use Admin -> System Check to confirm paths, or move data using the migration prompts.");
        }
    }

    private static async Task OfferDatabaseMigrationAsync(
        XamlRoot xamlRoot,
        IAppStoragePaths paths,
        IAppStorageMigrationService migration)
    {
        if (File.Exists(paths.DatabasePath) && new FileInfo(paths.DatabasePath).Length > 0)
        {
            return;
        }

        var legacy = migration.FindLegacyDatabasePaths();
        if (legacy.Count == 0)
        {
            return;
        }

        var source = legacy[0];
        var choice = await ShowYesNoAsync(
            xamlRoot,
            "Move database to Documents?",
            $"An existing database was found at:\n{source}\n\nCopy it to the permanent data folder?\n{paths.DatabasePath}");

        if (choice)
        {
            await migration.CopyDatabaseAsync(source).ConfigureAwait(true);
        }
    }

    private static async Task OfferSquareConfigMigrationAsync(
        XamlRoot xamlRoot,
        IAppStorageMigrationService migration)
    {
        var paths = App.Services.GetRequiredService<IAppStoragePaths>();
        if (File.Exists(paths.SquareConfigPath))
        {
            return;
        }

        var legacy = migration.FindLegacySquareConfigPaths();
        if (legacy.Count == 0)
        {
            return;
        }

        var source = legacy[0];
        var choice = await ShowYesNoAsync(
            xamlRoot,
            "Copy Square config?",
            $"Found square_config.json at:\n{source}\n\nCopy to:\n{paths.SquareConfigPath}?");

        if (choice)
        {
            await migration.CopySquareConfigAsync(source).ConfigureAwait(true);
        }
    }

    private static async Task OfferProductImagesMigrationAsync(
        XamlRoot xamlRoot,
        IAppStoragePaths paths,
        IAppStorageMigrationService migration,
        IWindowHandleProvider window)
    {
        if (Directory.EnumerateFiles(paths.ImagesFolder).Any())
        {
            return;
        }

        var legacyFolders = migration.FindLegacyProductImageFolders();
        if (legacyFolders.Count == 0)
        {
            return;
        }

        var defaultFolder = legacyFolders[0];
        var choice = await ShowYesNoAsync(
            xamlRoot,
            "Copy product images?",
            $"Product images were found in:\n{defaultFolder}\n\nCopy them to:\n{paths.ImagesFolder}?\n\nChoose \"No\" to pick a different folder.");

        string? sourceFolder;
        if (choice)
        {
            sourceFolder = defaultFolder;
        }
        else
        {
            sourceFolder = await PickImageFolderAsync(window).ConfigureAwait(true);
        }

        if (!string.IsNullOrWhiteSpace(sourceFolder))
        {
            var count = await migration.CopyProductImagesFromFolderAsync(sourceFolder).ConfigureAwait(true);
            if (count > 0)
            {
                await ShowInfoAsync(xamlRoot, "Images copied", $"Copied {count} image(s) to {paths.ImagesFolder}.");
            }
        }
    }

    private static async Task<string?> PickImageFolderAsync(IWindowHandleProvider window)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");

        var hwnd = window.WindowHandle;
        if (hwnd != 0)
        {
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private static async Task<bool> ShowYesNoAsync(XamlRoot xamlRoot, string title, string content)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = content, TextWrapping = TextWrapping.WrapWholeWords },
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Primary,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private static async Task ShowInfoAsync(XamlRoot xamlRoot, string title, string content)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = content, TextWrapping = TextWrapping.WrapWholeWords },
            CloseButtonText = "OK",
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        await dlg.ShowAsync();
    }
}
