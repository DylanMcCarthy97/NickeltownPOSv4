using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.AddDrinks;

/// <summary>Manual + session-blended favourite ordering for the active tab.</summary>
internal static class AddDrinksFavoritesBlend
{
    public static async Task<List<long>> LoadEffectiveFavoriteIdsAsync(
        ITabBarFavoritesService barFavorites,
        string tabLegacyId,
        string? tabDisplayName,
        IReadOnlyDictionary<long, int> sessionFavoriteCounts,
        CancellationToken cancellationToken)
    {
        var ordered = await barFavorites
            .GetEffectiveFavoriteItemIdsOrderedAsync(
                tabLegacyId,
                tabDisplayName,
                sessionFavoriteCounts,
                cancellationToken)
            .ConfigureAwait(false);

        return ordered.ToList();
    }
}
