using System;

namespace NickeltownPOSV4.Services;

public sealed class TabWorkspaceRefreshBus : ITabWorkspaceRefreshBus
{
    public event EventHandler? RefreshRequested;

    public void RequestRefresh() => RefreshRequested?.Invoke(this, EventArgs.Empty);
}
