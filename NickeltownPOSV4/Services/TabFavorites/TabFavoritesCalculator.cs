using System.Collections.Generic;
using System.Linq;

namespace NickeltownPOSV4.Services.TabFavorites;

public static class TabFavoritesCalculator
{
    public const int DefaultMaxFavorites = 8;

    /// <summary>Auto favourites require strictly more than this many orders in the current month.</summary>
    public const int MinMonthlyOrdersForAutoFavorite = 5;

    public static bool QualifiesByMonthlyOrders(int monthlyCount) => monthlyCount > MinMonthlyOrdersForAutoFavorite;

    public static IReadOnlyList<long> GetEffectiveFavoriteItemIds(
        IReadOnlyList<(long ItemId, int Count)> historyCounts,
        IReadOnlyDictionary<long, int> sessionCounts,
        IReadOnlyList<long> manualFavoriteItemIds,
        int maxFavorites = DefaultMaxFavorites)
    {
        var monthlyCounts = new Dictionary<long, int>();
        foreach (var (itemId, count) in historyCounts)
        {
            if (itemId > 0)
            {
                monthlyCounts[itemId] = count;
            }
        }

        var manualIds = new HashSet<long>();
        var scores = new Dictionary<long, double>();
        foreach (var id in manualFavoriteItemIds)
        {
            if (id > 0)
            {
                manualIds.Add(id);
                scores[id] = 1000.0;
            }
        }

        foreach (var (itemId, count) in historyCounts)
        {
            if (itemId <= 0 || !QualifiesByMonthlyOrders(count))
            {
                continue;
            }

            if (!scores.ContainsKey(itemId))
            {
                scores[itemId] = 0;
            }

            scores[itemId] += count;
        }

        if (sessionCounts is not null)
        {
            foreach (var kv in sessionCounts)
            {
                if (kv.Key <= 0)
                {
                    continue;
                }

                var monthly = monthlyCounts.GetValueOrDefault(kv.Key);
                if (!manualIds.Contains(kv.Key) && !QualifiesByMonthlyOrders(monthly))
                {
                    continue;
                }

                if (!scores.ContainsKey(kv.Key))
                {
                    scores[kv.Key] = 0;
                }

                scores[kv.Key] += kv.Value * 2.0;
            }
        }

        return scores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => kv.Key)
            .Take(maxFavorites)
            .ToList();
    }
}