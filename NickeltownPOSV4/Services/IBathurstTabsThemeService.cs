using System;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

/// <summary>Bar Tabs-only Bathurst race-day theme. Independent of the global POS appearance.</summary>
public interface IBathurstTabsThemeService
{
    bool IsEnabled { get; }

    event EventHandler? Changed;

    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    Task ReloadAsync(CancellationToken cancellationToken = default);
}
