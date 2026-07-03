namespace NickeltownPOSV4.Services;

public sealed class EditTabSession : IEditTabSession
{
    private readonly ITabPanelTargetState _target;

    public EditTabSession(ITabPanelTargetState target) => _target = target;

    public string? TabLegacyId
    {
        get => _target.TabLegacyId;
        set => _target.TabLegacyId = value;
    }

    public void Clear() => _target.ClearIdentity();
}
