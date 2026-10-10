using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.Services;

public sealed class BathurstTabsThemeService : IBathurstTabsThemeService
{
    private readonly IAppSettingsRepository _settings;
    private bool _enabled;

    public BathurstTabsThemeService(IAppSettingsRepository settings)
    {
        _settings = settings;
        try
        {
            var stored = settings.GetAsync<BathurstTabsThemeSettings>(BathurstTabsTheme.SettingsKey)
                .GetAwaiter()
                .GetResult();
            _enabled = stored?.Enabled ?? false;
        }
        catch
        {
            _enabled = false;
        }

        BathurstTabsTheme.IsEnabled = _enabled;
    }

    public bool IsEnabled => _enabled;

    public event EventHandler? Changed;

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (_enabled == enabled)
        {
            BathurstTabsTheme.IsEnabled = _enabled;
            return;
        }

        _enabled = enabled;
        BathurstTabsTheme.IsEnabled = enabled;
        await _settings.SetAsync(
                BathurstTabsTheme.SettingsKey,
                new BathurstTabsThemeSettings { Enabled = enabled },
                isSecret: false,
                cancellationToken)
            .ConfigureAwait(true);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        bool enabled;
        try
        {
            var stored = await _settings.GetAsync<BathurstTabsThemeSettings>(BathurstTabsTheme.SettingsKey, cancellationToken)
                .ConfigureAwait(true);
            enabled = stored?.Enabled ?? false;
        }
        catch
        {
            enabled = false;
        }

        if (_enabled == enabled)
        {
            BathurstTabsTheme.IsEnabled = _enabled;
            return;
        }

        _enabled = enabled;
        BathurstTabsTheme.IsEnabled = enabled;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
