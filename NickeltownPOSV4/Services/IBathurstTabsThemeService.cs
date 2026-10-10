using System;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

/// <summary>
/// Installation-level Bar Tabs Bathurst preference. Admins toggle; every user sees the same value.
/// Does not change global Appearance themes.
/// </summary>
public interface IBathurstTabsThemeService
{
    bool IsEnabled { get; }

    bool CanToggle { get; }

    event EventHandler? Changed;

    /// <summary>Load shared preference (default Normal). Safe to call repeatedly.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin-only. Persists installation-wide; logout must not clear it.</summary>
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task ReloadAsync(CancellationToken cancellationToken = default);
}
