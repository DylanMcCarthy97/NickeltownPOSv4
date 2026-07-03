using System;

namespace NickeltownPOSV4.Services;

/// <summary>Signals the tabs workspace to reload from SQLite after bar workflows complete.</summary>
public interface ITabWorkspaceRefreshBus
{
    event EventHandler? RefreshRequested;

    void RequestRefresh();
}
