using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Tabs;

public sealed class EditTabPanelHost : IEditTabPanelHost
{
    private readonly TabsWorkspaceViewModel _workspace;

    public EditTabPanelHost(TabsWorkspaceViewModel workspace) => _workspace = workspace;

    public bool CanDeleteCurrentTab => _workspace.CanDeleteSelectedForEditPanel();

    public void RequestDeleteCurrentTab() => _workspace.RequestDeleteFromEditPanel();
}