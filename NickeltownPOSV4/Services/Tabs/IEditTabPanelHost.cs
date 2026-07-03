namespace NickeltownPOSV4.Services.Tabs;

/// <summary>Bridges Edit Tab panel actions to the tabs workspace (delete, etc.).</summary>
public interface IEditTabPanelHost
{
    bool CanDeleteCurrentTab { get; }

    void RequestDeleteCurrentTab();
}