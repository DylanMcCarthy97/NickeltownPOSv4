using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public interface ITabBarFavoritesHistoryQuery
{
    Task<IReadOnlyList<(long ItemId, int Count)>> GetFavoriteItemCountsForTabAsync(
        string tabLegacyId,
        string? tabDisplayName,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}