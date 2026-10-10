using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services.Tabs;

public sealed class TabsVisualThemeService : ITabsVisualThemeService
{
    private readonly IAppSettingsRepository _settings;
    private bool _preferred;
    private bool _tabsRouteActive;
    private bool _loaded;

    public TabsVisualThemeService(IAppSettingsRepository settings) => _settings = settings;

    public bool IsBathurstPreferred => _preferred;

    public bool IsTabsRouteActive => _tabsRouteActive;

    public bool IsBathurstVisualActive => _preferred && _tabsRouteActive;

    public event EventHandler? Changed;

    public void SetTabsRouteActive(bool active)
    {
        if (_tabsRouteActive == active)
        {
            return;
        }

        _tabsRouteActive = active;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        var prefs = await _settings
            .GetAsync<TabsVisualThemePreferences>(TabsVisualThemePreferences.SettingsKey, cancellationToken)
            .ConfigureAwait(false);
        _preferred = prefs?.IsBathurstTheme ?? false;
        _loaded = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetBathurstPreferredAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (_preferred == enabled && _loaded)
        {
            return;
        }

        _preferred = enabled;
        _loaded = true;
        await _settings
            .SetAsync(
                TabsVisualThemePreferences.SettingsKey,
                new TabsVisualThemePreferences { IsBathurstTheme = enabled },
                isSecret: false,
                cancellationToken)
            .ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
