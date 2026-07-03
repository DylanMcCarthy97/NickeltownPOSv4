using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Tabs;

public sealed class TabsManagementUndoService : ITabsManagementUndoService
{
    private readonly ITabWorkspaceUndoStack _undo;
    private readonly ITabManagementRepository _tabManagement;
    private readonly ITabWorkspaceRefreshBus _refreshBus;

    public TabsManagementUndoService(
        ITabWorkspaceUndoStack undo,
        ITabManagementRepository tabManagement,
        ITabWorkspaceRefreshBus refreshBus)
    {
        _undo = undo;
        _tabManagement = tabManagement;
        _refreshBus = refreshBus;
    }

    public void RegisterArchiveUndo(string tabLegacyId, string label)
    {
        _undo.PushUndo(
            $"Undo archive ({label})",
            async () =>
            {
                var back = await _tabManagement.SetTabArchivedAsync(tabLegacyId, false).ConfigureAwait(false);
                if (!back.Ok)
                {
                    return false;
                }

                _refreshBus.RequestRefresh();
                return true;
            });
    }

    public void RegisterSoftDeleteUndo(string tabLegacyId, string label)
    {
        _undo.PushUndo(
            $"Undo remove ({label})",
            async () =>
            {
                var back = await _tabManagement.RestoreSoftDeletedTabAsync(tabLegacyId).ConfigureAwait(false);
                if (!back.Ok)
                {
                    return false;
                }

                _refreshBus.RequestRefresh();
                return true;
            });
    }
}
