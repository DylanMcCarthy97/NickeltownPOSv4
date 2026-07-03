using System.Collections.Generic;

namespace NickeltownPOSV4.Services;

/// <summary>Target tab for the Add Drinks slide panel (set by Tabs workspace before opening the panel).</summary>
public interface IAddDrinksSession
{
    string? TargetTabLegacyId { get; set; }

    string? TargetTabDisplayName { get; set; }

    /// <summary>Tab balance when workspace opened (display only).</summary>
    decimal? TargetTabBalance { get; set; }

    /// <summary>When true, bar catalog uses guest/special guest pricing (V2-style).</summary>
    bool TargetTabIsGuest { get; set; }

    /// <summary>Per-tab session drink counts for V2 favourites blend (cleared when session clears).</summary>
    IReadOnlyDictionary<long, int> SessionFavoriteCounts { get; }

    void RecordSessionFavorite(long itemId, int quantityAdded = 1);

    void Clear();
}
