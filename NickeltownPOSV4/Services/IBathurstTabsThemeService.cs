using System;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

/// <summary>Bar Tabs race-day overlay, driven by the Bathurst 1000 Appearances theme.</summary>
public interface IBathurstTabsThemeService
{
    bool IsEnabled { get; }

    event EventHandler? Changed;

    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task ReloadAsync(CancellationToken cancellationToken = default);
}
