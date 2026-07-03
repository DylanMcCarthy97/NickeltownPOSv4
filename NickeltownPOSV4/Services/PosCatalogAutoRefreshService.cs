using System;
using Microsoft.UI.Dispatching;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services;

/// <summary>
/// Periodically invalidates cached bar catalog data and notifies POS surfaces to reload
/// (Add Drinks, Pitstop, tabs list).
/// </summary>
public sealed class PosCatalogAutoRefreshService
{
    /// <summary>How often open POS screens pick up stock/pricing/special changes.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

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
        _refreshBus.RequestRefresh();
    }
}