using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.AddDrinks;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services;

public sealed class BootstrapResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public static BootstrapResult Success() => new() { Ok = true };

    public static BootstrapResult Fail(string message) => new() { Ok = false, ErrorMessage = message };
}

public interface IAppBootstrapService
{
    Task<BootstrapResult> RunAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class AppBootstrapService : IAppBootstrapService
{
    private readonly IAppStoragePaths _storagePaths;
    private readonly AppDatabase _database;
    private readonly IDefaultStaffBootstrapper _staffBootstrap;
    private readonly IStaffPinLookupCache _pinCache;
    private readonly IShotMixerBootstrapper _shotMixer;
    private readonly IBackupService _backup;
    private readonly Microsoft.Extensions.Logging.ILogger<AppBootstrapService> _logger;

    public AppBootstrapService(
        IAppStoragePaths storagePaths,
        AppDatabase database,
        IDefaultStaffBootstrapper staffBootstrap,
        IStaffPinLookupCache pinCache,
        IShotMixerBootstrapper shotMixer,
        IBackupService backup,
        Microsoft.Extensions.Logging.ILogger<AppBootstrapService> logger)
    {
        _storagePaths = storagePaths;
        _database = database;
        _staffBootstrap = staffBootstrap;
        _pinCache = pinCache;
        _shotMixer = shotMixer;
        _backup = backup;
        _logger = logger;
    }

    public async Task<BootstrapResult> RunAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report("Preparing data folders…");
            _storagePaths.EnsureDirectories();

            progress?.Report("Preparing database…");
            await _database.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            progress?.Report("Checking staff accounts…");
            await _staffBootstrap.EnsureDefaultStaffIfEmptyAsync(cancellationToken).ConfigureAwait(false);

            progress?.Report("Preparing sign-in…");
            _pinCache.Refresh(cancellationToken);

            progress?.Report("Loading bar configuration…");
            await _shotMixer.EnsureAsync(cancellationToken).ConfigureAwait(false);

            progress?.Report("Starting backup…");
            _ = _backup.CreateAutomaticBackupAsync("App startup");

            return BootstrapResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application bootstrap failed.");
            return BootstrapResult.Fail(PosUserMessage.FromException(ex, "Startup failed. Check logs and restart."));
        }
    }
}
