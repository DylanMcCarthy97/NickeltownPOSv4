using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;

namespace NickeltownPOSV4.Services.Updates;

public static class AppUpdateRestartHelper
{
    private const string MarkerFileName = "pending-update-notification.json";
    private const string InstallOkFileName = "update-install-ok.flag";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static bool _relaunchArmed;

    private sealed record PendingUpdateNotification(string Version);

    /// <summary>
    /// Must run <em>before</em> <c>AddPackageAsync</c> with <c>ForceApplicationShutdown</c>.
    /// Windows may kill this process mid-install; the watchdog relaunches only if install-ok is set.
    /// </summary>
    public static void ArmRelaunchBeforeInstall(string installedVersion)
    {
        WriteMarker(installedVersion);
        ClearInstallOkFlag();
        ScheduleWatchdogRelaunch(installedVersion.Trim(), Process.GetCurrentProcess().Id);
        // Optimistic: install is about to run. Cleared again if deploy fails while we still live.
        WriteInstallOkFlag();
        _relaunchArmed = true;
        Log($"Armed post-update relaunch for version {installedVersion.Trim()}.");
    }

    /// <summary>Call if install fails after arming so the watchdog will not relaunch.</summary>
    public static void CancelArmedRelaunch()
    {
        ClearInstallOkFlag();
        _relaunchArmed = false;
        Log("Cancelled armed post-update relaunch (install failed).");
    }

    /// <summary>
    /// Call after a successful install if this process is still alive.
    /// Prefer platform restart; otherwise exit and let the already-armed watchdog relaunch.
    /// </summary>
    public static void FinishUpdateAndExit(string installedVersion)
    {
        if (!_relaunchArmed)
        {
            ArmRelaunchBeforeInstall(installedVersion);
        }
        else
        {
            WriteInstallOkFlag();
        }

        try
        {
            var restartResult = AppInstance.Restart(string.Empty);
            Log($"AppInstance.Restart returned {restartResult}.");
            if (restartResult == AppRestartFailureReason.RestartPending)
            {
                Application.Current.Exit();
                return;
            }
        }
        catch (Exception ex)
        {
            Log($"AppInstance.Restart threw: {ex.Message}");
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

        await AppUpdateDialogFactory.CreateCompleteDialog(xamlRoot, version).ShowAsync();
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
            ClearInstallOkFlag();
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

    private static string GetInstallOkPath()
    {
        var paths = App.Services.GetRequiredService<IAppStoragePaths>();
        paths.EnsureDirectories();
        return Path.Combine(paths.ConfigFolder, InstallOkFileName);
    }

    private static void WriteInstallOkFlag()
    {
        try
        {
            File.WriteAllText(GetInstallOkPath(), DateTimeOffset.Now.ToString("O"));
        }
        catch (Exception ex)
        {
            Log($"Could not write install-ok flag: {ex.Message}");
        }
    }

    private static void ClearInstallOkFlag()
    {
        try
        {
            var path = GetInstallOkPath();
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

    /// <summary>
    /// Out-of-process watchdog: wait for this PID to exit, require install-ok, settle, then relaunch via AUMID.
    /// </summary>
    private static void ScheduleWatchdogRelaunch(string expectedVersion, int waitPid)
    {
        if (!AppVersionInfo.IsPackaged)
        {
            Log("Not packaged — skipping watchdog relaunch.");
            return;
        }

        var aumid = TryGetAppUserModelId();
        if (string.IsNullOrWhiteSpace(aumid))
        {
            Log("Could not resolve AUMID — skipping watchdog relaunch.");
            return;
        }

        string installOkPath;
        try
        {
            installOkPath = GetInstallOkPath();
        }
        catch (Exception ex)
        {
            Log($"Could not resolve install-ok path: {ex.Message}");
            return;
        }

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "NickeltownPOS-updates");
            Directory.CreateDirectory(tempDir);
            var scriptPath = Path.Combine(tempDir, "relaunch-after-update.ps1");
            File.WriteAllText(scriptPath, BuildWatchdogScript(), Encoding.UTF8);

            var args =
                $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" "
                + $"-WaitPid {waitPid} -Aumid \"{aumid}\" -ExpectedVersion \"{expectedVersion}\" "
                + $"-InstallOkPath \"{installOkPath}\"";

            var started = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            if (started is null)
            {
                Log("Failed to start PowerShell watchdog; falling back to cmd.");
                ScheduleCmdFallbackRelaunch(aumid);
                return;
            }

            Log($"Started update watchdog PID {started.Id} (waits for POS PID {waitPid}).");
        }
        catch (Exception ex)
        {
            Log($"Watchdog start failed ({ex.Message}); falling back to cmd.");
            ScheduleCmdFallbackRelaunch(aumid);
        }
    }

    private static string BuildWatchdogScript() =>
        """
        param(
          [Parameter(Mandatory = $true)][int]$WaitPid,
          [Parameter(Mandatory = $true)][string]$Aumid,
          [Parameter(Mandatory = $true)][string]$InstallOkPath,
          [Parameter(Mandatory = $false)][string]$ExpectedVersion = ""
        )

        $ErrorActionPreference = "SilentlyContinue"

        # Wait until the POS process exits (or ~3 minutes).
        $deadline = (Get-Date).AddSeconds(180)
        while ((Get-Date) -lt $deadline) {
          if (-not (Get-Process -Id $WaitPid -ErrorAction SilentlyContinue)) { break }
          Start-Sleep -Seconds 1
        }

        # Only relaunch after a successful install arm.
        if (-not (Test-Path -LiteralPath $InstallOkPath)) {
          exit 0
        }

        # Allow MSIX registration / ForceApplicationShutdown to finish.
        Start-Sleep -Seconds 5

        $family = ($Aumid -split "!")[0]
        if ($ExpectedVersion) {
          $verDeadline = (Get-Date).AddSeconds(90)
          while ((Get-Date) -lt $verDeadline) {
            $pkg = Get-AppxPackage | Where-Object { $_.PackageFamilyName -eq $family } | Select-Object -First 1
            if ($pkg) {
              try {
                $installed = [version]$pkg.Version.ToString()
                $want = [version]$ExpectedVersion
                if ($installed -ge $want) { break }
              } catch {
                break
              }
            }
            Start-Sleep -Seconds 2
          }
        } else {
          Start-Sleep -Seconds 3
        }

        Start-Process "explorer.exe" "shell:AppsFolder\$Aumid"
        Start-Sleep -Seconds 8
        $running = Get-Process -Name "NickeltownPOSV4" -ErrorAction SilentlyContinue
        if (-not $running) {
          Start-Process "explorer.exe" "shell:AppsFolder\$Aumid"
        }

        Remove-Item -LiteralPath $InstallOkPath -Force -ErrorAction SilentlyContinue
        """;

    private static void ScheduleCmdFallbackRelaunch(string aumid)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments =
                    $"/c ping localhost -n 12 >nul & explorer.exe shell:AppsFolder\\{aumid}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch (Exception ex)
        {
            Log($"Cmd fallback relaunch failed: {ex.Message}");
        }
    }

    private static string? TryGetAppUserModelId()
    {
        try
        {
            var familyName = Windows.ApplicationModel.Package.Current.Id.FamilyName;
            if (string.IsNullOrWhiteSpace(familyName))
            {
                return null;
            }

            // Matches Application Id="App" in Package.appxmanifest.
            return $"{familyName}!App";
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static void Log(string message)
    {
        Debug.WriteLine("[AppUpdateRestart] " + message);
        try
        {
            var logger = App.Services.GetService<ILoggerFactory>()?.CreateLogger("AppUpdateRestart");
            logger?.LogInformation("{Message}", message);
        }
        catch
        {
            // Logging must never break update flow.
        }
    }
}
