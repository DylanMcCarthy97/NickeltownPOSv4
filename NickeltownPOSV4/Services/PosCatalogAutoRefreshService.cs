using System;
using Microsoft.UI.Dispatching;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services;

/// <summary>
/// Periodically invalidates cached bar catalog data and reloads open drink lists.
/// Does not rebuild the tabs board — that only refreshes after a real tab change.
/// </summary>
public sealed class PosCatalogAutoRefreshService
{
    /// <summary>How often open drink screens pick up stock or price changes.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(90);

    private readonly ITabWorkspaceRefreshBus _refreshBus;
    private readonly IBarCatalogCache _barCatalogCache;

    private DispatcherQueueTimer? _timer;

    public PosCatalogAutoRefreshService(
        ITabWorkspaceRefreshBus refreshBus,
        IBarCatalogCache barCatalogCache)
    {
        _refreshBus = refreshBus;
        _barCatalogCache = barCatalogCache;
    }

    public void Start(DispatcherQueue dispatcher)
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = dispatcher.CreateTimer();
        _timer.Interval = RefreshInterval;
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => PulseRefresh();
        _timer.Start();
    }

    public void PulseRefresh()
    {
        _barCatalogCache.Invalidate();
        _refreshBus.RequestCatalogRefresh();
    }
}