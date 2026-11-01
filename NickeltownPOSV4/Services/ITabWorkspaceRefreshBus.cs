using System;

namespace NickeltownPOSV4.Services;

/// <summary>Signals the tabs workspace and drink lists to reload after bar work, or just the drink lists.</summary>
public interface ITabWorkspaceRefreshBus
{
    event EventHandler? RefreshRequested;

    event EventHandler? CatalogRefreshRequested;

    void RequestRefresh();

    void RequestCatalogRefresh();
}
