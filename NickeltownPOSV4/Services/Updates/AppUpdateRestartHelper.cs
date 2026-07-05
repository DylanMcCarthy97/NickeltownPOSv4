using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;

namespace NickeltownPOSV4.Services.Updates;

public static class AppUpdateRestartHelper
{
    private const string MarkerFileName = "pending-update-notification.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record PendingUpdateNotification(string Version);

    public static void ScheduleRestartWithNotification(string installedVersion)
    {
        WriteMarker(installedVersion);

        var restartResult = AppInstance.Restart(string.Empty);
        if (restartResult == AppRestartFailureReason.RestartPending)
        {
            return;
        }

        Application.Current.Exit();
    }

    public static async Task ShowUpdatedNotificationIfNeededAsync(XamlRoot xamlRoot)
    {
        var version = TryConsumePendingNotification();
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Update complete",
            Content = $"Nickeltown POS has been updated to version {version.Trim()}.",
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        await dlg.ShowAsync();
    }

    private static string? TryConsumePendingNotification()
    {
        var path = GetMarkerPath();
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var notification = JsonSerializer.Deserialize<PendingUpdateNotification>(json, JsonOptions);
            return string.IsNullOrWhiteSpace(notification?.Version) ? null : notification.Version.Trim();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            TryDeleteMarker(path);
        }
    }

    private static void WriteMarker(string version)
    {
        var paths = App.Services.GetRequiredService<IAppStoragePaths>();
        paths.EnsureDirectories();

        var payload = JsonSerializer.Serialize(new PendingUpdateNotification(version.Trim()), JsonOptions);
        File.WriteAllText(GetMarkerPath(paths), payload);
    }

    private static string GetMarkerPath()
    {
        var paths = App.Services.GetRequiredService<IAppStoragePaths>();
        return GetMarkerPath(paths);
    }

    private static string GetMarkerPath(IAppStoragePaths paths) =>
        Path.Combine(paths.ConfigFolder, MarkerFileName);

    private static void TryDeleteMarker(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}