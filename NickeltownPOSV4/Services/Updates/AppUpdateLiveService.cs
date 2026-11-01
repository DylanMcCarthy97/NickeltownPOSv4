using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services.Updates;

/// <summary>
/// Looks for a new till build while the app is open.
/// Fits it on the login screen so a sale is never interrupted.
/// </summary>
public sealed class AppUpdateLiveService
{
    private readonly IAppUpdateConfigService _config;
    private readonly IAppUpdateService _updates;
    private readonly IUserSessionService _session;
    private readonly IWindowHandleProvider _window;
    private readonly ILogger<AppUpdateLiveService> _logger;

    private DispatcherQueue? _dispatcher;
    private DispatcherQueueTimer? _timer;
    private bool _busy;
    private bool _pending;

    public AppUpdateLiveService(
        IAppUpdateConfigService config,
        IAppUpdateService updates,
        IUserSessionService session,
        IWindowHandleProvider window,
        ILogger<AppUpdateLiveService> logger)
    {
        _config = config;
        _updates = updates;
        _session = session;
        _window = window;
        _logger = logger;
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    public void Start(DispatcherQueue dispatcher)
    {
        if (_timer is not null)
        {
            return;
        }

        _dispatcher = dispatcher;
        _timer = dispatcher.CreateTimer();
        _timer.Interval = AppUpdateLiveRules.FirstCheckDelay;
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) =>
        {
            if (_timer is not null)
            {
                _timer.Interval = AppUpdateLiveRules.CheckInterval;
            }

            _ = CheckAndMaybeInstallAsync();
        };
        _timer.Start();
    }

    internal Task CheckNowAsync() => CheckAndMaybeInstallAsync();

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(IUserSessionService.IsSignedIn) or null or ""))
        {
            return;
        }

        if (_session.IsSignedIn || !_pending)
        {
            return;
        }

        _ = CheckAndMaybeInstallAsync();
    }

    private async Task CheckAndMaybeInstallAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            var cfg = await _config.LoadAsync().ConfigureAwait(true);
            if (!AppUpdateLiveRules.ShouldKeepLooking(cfg.CheckOnStartup))
            {
                return;
            }

            var check = await _updates.CheckForUpdateAsync().ConfigureAwait(true);
            if (!check.UpdateAvailable || check.Manifest is null)
            {
                _pending = false;
                return;
            }

            _pending = true;
            if (!AppUpdateLiveRules.CanInstallNow(cfg.AutoInstall, _session.IsSignedIn))
            {
                return;
            }

            var root = _window.GetXamlRoot();
            if (root is null)
            {
                return;
            }

            var installed = await AppUpdateUiHelper
                .TryPromptAndInstallAsync(root, autoInstall: true)
                .ConfigureAwait(true);
            if (installed)
            {
                _pending = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live update check failed.");
        }
        finally
        {
            _busy = false;
        }
    }
}
