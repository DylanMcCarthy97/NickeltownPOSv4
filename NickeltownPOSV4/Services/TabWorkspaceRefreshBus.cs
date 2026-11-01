using System;

namespace NickeltownPOSV4.Services;

public sealed class TabWorkspaceRefreshBus : ITabWorkspaceRefreshBus
{
    public event EventHandler? RefreshRequested;

    public event EventHandler? CatalogRefreshRequested;

    public void RequestRefresh()
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
        CatalogRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestCatalogRefresh() => CatalogRefreshRequested?.Invoke(this, EventArgs.Empty);
}
