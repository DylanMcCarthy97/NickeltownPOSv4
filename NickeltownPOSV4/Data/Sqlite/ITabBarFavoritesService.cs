using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public interface ITabBarFavoritesService
{
    Task<IReadOnlyList<long>> GetManualFavoriteItemIdsOrderedAsync(string tabLegacyId, CancellationToken cancellationToken = default);

    /// <summary>Manual pins + current-month history (&gt;5 orders) + session counts (TabFavoritesCalculator).</summary>
    Task<IReadOnlyList<long>> GetEffectiveFavoriteItemIdsOrderedAsync(
        string tabLegacyId,
        string? tabDisplayName,
        IReadOnlyDictionary<long, int> sessionCounts,
        CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(string tabLegacyId, long itemId, bool favorite, CancellationToken cancellationToken = default);
}
