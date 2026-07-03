using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Services;

/// <summary>Sets shared tab identity on <see cref="ITabPanelTargetState"/> before opening slide panels.</summary>
public interface ITabPanelTargetBinder
{
    void Bind(
        string? tabLegacyId,
        string? displayName,
        decimal? displayBalance = null,
        bool isGuest = false);

    void BindFromTab(TabCardModel tab, bool withGuestFlag = false);
}
