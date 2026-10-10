using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.Services;

/// <summary>Keeps the Bar Tabs race-day overlay in sync with the Appearances theme.</summary>
public sealed class BathurstTabsThemeService : IBathurstTabsThemeService
{
    private bool _enabled;

    public BathurstTabsThemeService()
    {
        SyncFromAppearance();
    }

    public bool IsEnabled => _enabled;

    public event EventHandler? Changed;

    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        ApplyEnabled(enabled);
        return Task.CompletedTask;
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        SyncFromAppearance();
        return Task.CompletedTask;
    }

    private void SyncFromAppearance() =>
        ApplyEnabled(AppTheme.CurrentThemeId == UiThemeId.Bathurst1000);

    private void ApplyEnabled(bool enabled)
    {
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
