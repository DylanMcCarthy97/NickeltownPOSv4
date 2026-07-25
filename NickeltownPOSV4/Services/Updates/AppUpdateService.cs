using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NickeltownPOSV4.Services.Settings;
using Windows.Management.Deployment;

namespace NickeltownPOSV4.Services.Updates;

public sealed class AppUpdateService : IAppUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HttpClient Http = new()
    {
        // MSIX packages are ~100MB; keep headroom for slow venue Wi‑Fi.
        Timeout = TimeSpan.FromMinutes(10),
    };

    private readonly IAppUpdateConfigService _config;
    private readonly ILogger<AppUpdateService> _logger;

    public AppUpdateService(IAppUpdateConfigService config, ILogger<AppUpdateService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<AppUpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (!AppVersionInfo.IsPackaged)
        {
            return AppUpdateCheckResult.Skipped("Updates require the MSIX-installed app (not folder publish).");
        }

        var cfg = await _config.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(cfg.FeedBaseUrl))
        {
            return AppUpdateCheckResult.Skipped("No update feed configured.");
        }

        try
        {
            var manifestPath = BuildManifestPath(cfg.FeedBaseUrl);
            var manifest = await LoadManifestAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                return AppUpdateCheckResult.Failed("Update manifest missing or invalid.");
            }

            if (string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.PackageUri))
            {
                return AppUpdateCheckResult.Failed("Update manifest is incomplete.");
            }

            manifest.PackageUri = ResolvePackageUri(cfg.FeedBaseUrl, manifest.PackageUri);

            if (!AppVersionInfo.IsRemoteNewer(manifest.Version))
            {
                return AppUpdateCheckResult.None();
            }

            return AppUpdateCheckResult.Available(manifest);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
            return AppUpdateCheckResult.Failed("Could not check for updates. Try again later.");
        }
    }

    public async Task<AppUpdateInstallResult> InstallUpdateAsync(
        AppUpdateManifest manifest,
        IProgress<AppUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!AppVersionInfo.IsPackaged)
        {
            return AppUpdateInstallResult.Fail("Install updates using the MSIX package on this PC.");
        }

        if (string.IsNullOrWhiteSpace(manifest.PackageUri))
        {
            return AppUpdateInstallResult.Fail("Update package location is missing.");
        }

        try
        {
            progress?.Report(new AppUpdateProgress(AppUpdateStage.PullIn, "Starting the download…", 0));
            var localMsix = await DownloadPackageAsync(manifest.PackageUri, progress, cancellationToken).ConfigureAwait(false);

            // Arm out-of-process relaunch BEFORE AddPackageAsync. ForceApplicationShutdown may
            // kill this process mid-deploy; the watchdog still relaunches after we exit.
            progress?.Report(new AppUpdateProgress(AppUpdateStage.FitBuild, "Getting ready to install…"));
            AppUpdateRestartHelper.ArmRelaunchBeforeInstall(manifest.Version);

            progress?.Report(new AppUpdateProgress(AppUpdateStage.FitBuild, "Fitting the new build…"));
            var pm = new PackageManager();
            var options = DeploymentOptions.ForceApplicationShutdown
                | DeploymentOptions.ForceUpdateFromAnyVersion;

            var deployResult = await pm.AddPackageAsync(
                new Uri(localMsix),
                null,
                options);

            if (string.IsNullOrWhiteSpace(deployResult.ErrorText))
            {
                _logger.LogInformation("Installed update {Version} from {Package}", manifest.Version, localMsix);
                TryDeleteFile(localMsix);
                progress?.Report(new AppUpdateProgress(AppUpdateStage.Restart, "Closing the till for restart…"));
                return AppUpdateInstallResult.Success(shutdown: true);
            }

            AppUpdateRestartHelper.CancelArmedRelaunch();
            return AppUpdateInstallResult.Fail(deployResult.ErrorText);
        }
        catch (Exception ex)
        {
            AppUpdateRestartHelper.CancelArmedRelaunch();
            _logger.LogError(ex, "Update install failed.");
            return AppUpdateInstallResult.Fail("Update install failed. Check logs and try again.");
        }
    }

    internal static string BuildManifestPath(string feedBaseUrl)
    {
        var basePath = feedBaseUrl.Trim().TrimEnd('/', '\\');
        if (basePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || basePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return basePath + "/update-manifest.json";
        }

        return Path.Combine(basePath, "update-manifest.json");
    }

    internal static string ResolvePackageUri(string feedBaseUrl, string packageUri)
    {
        if (Uri.TryCreate(packageUri, UriKind.Absolute, out var absolute)
            && (absolute.IsFile || absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.AbsoluteUri;
        }

        var basePath = feedBaseUrl.Trim().TrimEnd('/', '\\');
        if (basePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || basePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return basePath + "/" + packageUri.TrimStart('/');
        }

        return Path.GetFullPath(Path.Combine(basePath, packageUri));
    }

    private static async Task<AppUpdateManifest?> LoadManifestAsync(string manifestPath, CancellationToken cancellationToken)
    {
        string json;
        if (manifestPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || manifestPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            json = await Http.GetStringAsync(new Uri(manifestPath), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (!File.Exists(manifestPath))
            {
                return null;
            }

            json = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        }

        return JsonSerializer.Deserialize<AppUpdateManifest>(json, JsonOptions);
    }

    private static async Task<string> DownloadPackageAsync(
        string packageUri,
        IProgress<AppUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(packageUri);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "NickeltownPOS-update.msix";
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "NickeltownPOS-updates");
        Directory.CreateDirectory(tempDir);
        var dest = Path.Combine(tempDir, fileName);

        if (packageUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || packageUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var response = await Http.GetAsync(new Uri(packageUri), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var file = File.Create(dest);
            await CopyWithProgressAsync(stream, file, total, progress, cancellationToken).ConfigureAwait(false);
            return dest;
        }

        var source = packageUri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            ? new Uri(packageUri).LocalPath
            : packageUri;

        progress?.Report(new AppUpdateProgress(AppUpdateStage.PullIn, "Copying the build off the feed…"));
        File.Copy(source, dest, overwrite: true);
        progress?.Report(new AppUpdateProgress(AppUpdateStage.PullIn, "Build copied", 100));
        return dest;
    }

    private static async Task CopyWithProgressAsync(
        Stream source,
        Stream destination,
        long? totalBytes,
        IProgress<AppUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long received = 0;
        var lastReportedPercent = -1;
        var lastReport = DateTimeOffset.MinValue;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;

            if (progress is null)
            {
                continue;
            }

            var percent = totalBytes is > 0 ? (int)(received * 100 / totalBytes.Value) : -1;
            var now = DateTimeOffset.UtcNow;

            // Throttle so a fast LAN download does not flood the dispatcher.
            if (percent == lastReportedPercent && now - lastReport < TimeSpan.FromMilliseconds(400))
            {
                continue;
            }

            lastReportedPercent = percent;
            lastReport = now;

            progress.Report(percent >= 0
                ? new AppUpdateProgress(AppUpdateStage.PullIn, $"{Megabytes(received)} of {Megabytes(totalBytes!.Value)} downloaded", percent)
                : new AppUpdateProgress(AppUpdateStage.PullIn, $"{Megabytes(received)} downloaded"));
        }

        progress?.Report(new AppUpdateProgress(AppUpdateStage.PullIn, $"{Megabytes(received)} downloaded", 100));
    }

    private static string Megabytes(long bytes) =>
        (bytes / 1024d / 1024d).ToString("0.0", CultureInfo.CurrentCulture) + " MB";

    private static void TryDeleteFile(string path)
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
