using System;
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
        Timeout = TimeSpan.FromSeconds(30),
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
        IProgress<string>? progress = null,
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
            progress?.Report("Downloading update…");
            var localMsix = await DownloadPackageAsync(manifest.PackageUri, cancellationToken).ConfigureAwait(false);

            progress?.Report("Installing update…");
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
                return AppUpdateInstallResult.Success(shutdown: true);
            }

            return AppUpdateInstallResult.Fail(deployResult.ErrorText);
        }
        catch (Exception ex)
        {
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

    private static async Task<string> DownloadPackageAsync(string packageUri, CancellationToken cancellationToken)
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
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var file = File.Create(dest);
            await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            return dest;
        }

        var source = packageUri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
            ? new Uri(packageUri).LocalPath
            : packageUri;

        File.Copy(source, dest, overwrite: true);
        return dest;
    }
}
