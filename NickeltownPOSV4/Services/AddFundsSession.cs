namespace NickeltownPOSV4.Services;

public sealed class AddFundsSession : IAddFundsSession
{
    private readonly ITabPanelTargetState _target;

    public AddFundsSession(ITabPanelTargetState target) => _target = target;

    public string? TargetTabLegacyId
    {
        get => _target.TabLegacyId;
        set => _target.TabLegacyId = value;
    }

    public string? TargetTabDisplayName
    {
        get => _target.TabDisplayName;
        set => _target.TabDisplayName = value;
    }

    public decimal? TargetTabBalance
    {
        get => _target.DisplayBalance;
        set => _target.DisplayBalance = value;
    }

    public void Clear() => _target.ClearIdentity();
}
