using System.Collections.Generic;

namespace NickeltownPOSV4.Services;

public sealed class AddDrinksSession : IAddDrinksSession
{
    private readonly ITabPanelTargetState _target;
    private readonly Dictionary<long, int> _sessionFavoriteCounts = new();

    public AddDrinksSession(ITabPanelTargetState target) => _target = target;

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

    public bool TargetTabIsGuest
    {
        get => _target.IsGuestTab;
        set => _target.IsGuestTab = value;
    }

    public IReadOnlyDictionary<long, int> SessionFavoriteCounts => _sessionFavoriteCounts;

    public void RecordSessionFavorite(long itemId, int quantityAdded = 1)
    {
        if (itemId <= 0 || quantityAdded <= 0)
        {
            return;
        }

        _sessionFavoriteCounts[itemId] = _sessionFavoriteCounts.GetValueOrDefault(itemId) + quantityAdded;
    }

    public void Clear()
    {
        _target.ClearIdentity();
        _sessionFavoriteCounts.Clear();
    }
}
