namespace NickeltownPOSV4.Services;

public sealed class TabHistorySession : ITabHistorySession
{
    private readonly ITabPanelTargetState _target;

    public TabHistorySession(ITabPanelTargetState target) => _target = target;

    public string? TabLegacyId
    {
        get => _target.TabLegacyId;
        set => _target.TabLegacyId = value;
    }

    public string? TabDisplayName
    {
        get => _target.TabDisplayName;
        set => _target.TabDisplayName = value;
    }

    public void Clear() => _target.ClearIdentity();
}
