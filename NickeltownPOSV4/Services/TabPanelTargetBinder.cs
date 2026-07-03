using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Services;

public sealed class TabPanelTargetBinder : ITabPanelTargetBinder
{
    private readonly ITabPanelTargetState _target;

    public TabPanelTargetBinder(ITabPanelTargetState target) => _target = target;

    public void Bind(
        string? tabLegacyId,
        string? displayName,
        decimal? displayBalance = null,
        bool isGuest = false)
    {
        _target.TabLegacyId = tabLegacyId;
        _target.TabDisplayName = displayName;
        _target.DisplayBalance = displayBalance;
        _target.IsGuestTab = isGuest;
    }

    public void BindFromTab(TabCardModel tab, bool withGuestFlag = false) =>
        Bind(tab.Id, tab.DisplayName, tab.Balance, withGuestFlag && tab.IsGuest);
}
