namespace NickeltownPOSV4.Services.Tabs;

public interface ITabsManagementUndoService
{
    void RegisterArchiveUndo(string tabLegacyId, string label);

    void RegisterSoftDeleteUndo(string tabLegacyId, string label);
}
