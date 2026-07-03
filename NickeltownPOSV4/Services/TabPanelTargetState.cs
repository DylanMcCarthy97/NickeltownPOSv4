namespace NickeltownPOSV4.Services;

public sealed class TabPanelTargetState : ITabPanelTargetState
{
    public string? TabLegacyId { get; set; }

    public string? TabDisplayName { get; set; }

    public decimal? DisplayBalance { get; set; }

    public bool IsGuestTab { get; set; }

    public void ClearIdentity()
    {
        TabLegacyId = null;
        TabDisplayName = null;
        DisplayBalance = null;
        IsGuestTab = false;
    }

    public void ClearAll() => ClearIdentity();
}
