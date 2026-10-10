using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.Services;

/// <summary>Persists Bar Tabs Bathurst mode for the whole POS install. Admins only may change it.</summary>
public sealed class BathurstTabsThemeService : IBathurstTabsThemeService
{
    private readonly IAppSettingsRepository _settings;
    private readonly IUserSessionService _session;
    private bool _enabled;
    private bool _loaded;

    public BathurstTabsThemeService(IAppSettingsRepository settings, IUserSessionService session)
    {
        _settings = settings;
        _session = session;
        _session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IUserSessionService.IsAdmin) or nameof(IUserSessionService.IsSignedIn) or null or "")
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public bool IsEnabled => _enabled;

    public bool CanToggle => _session.IsSignedIn && _session.IsAdmin;

    public event EventHandler? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await ReloadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var prefs = await _settings
            .GetAsync<TabsVisualThemePreferences>(TabsVisualThemePreferences.SettingsKey, cancellationToken)
            .ConfigureAwait(false);

        // Migrate legacy key once if the new key is unset.
        if (prefs is null)
        {
            var legacy = await _settings
                .GetAsync<Models.BathurstTabsThemeSettings>(BathurstTabsTheme.LegacySettingsKey, cancellationToken)
                .ConfigureAwait(false);
            if (legacy is not null)
            {
                prefs = new TabsVisualThemePreferences { IsBathurstTheme = legacy.Enabled };
                await _settings
                    .SetAsync(TabsVisualThemePreferences.SettingsKey, prefs, isSecret: false, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        ApplyEnabled(prefs?.IsBathurstTheme ?? false, raiseEvenIfUnchanged: !_loaded);
        _loaded = true;
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (!CanToggle)
        {
            return;
        }

        ApplyEnabled(enabled, raiseEvenIfUnchanged: false);
        await _settings
            .SetAsync(
                TabsVisualThemePreferences.SettingsKey,
                new TabsVisualThemePreferences { IsBathurstTheme = enabled },
                isSecret: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void ApplyEnabled(bool enabled, bool raiseEvenIfUnchanged)
    {
        var changed = _enabled != enabled;
        _enabled = enabled;
        BathurstTabsTheme.IsEnabled = enabled;
        if (changed || raiseEvenIfUnchanged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
