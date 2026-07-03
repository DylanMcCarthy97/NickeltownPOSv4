using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteTabBarFavoritesHistoryQuery : ITabBarFavoritesHistoryQuery
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteTabBarFavoritesHistoryQuery(SqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<(long ItemId, int Count)>> GetFavoriteItemCountsForTabAsync(
        string tabLegacyId,
        string? tabDisplayName,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId)
            || !TabBoardRoute.TryParse(tabLegacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult<IReadOnlyList<(long ItemId, int Count)>>(Array.Empty<(long, int)>());
        }

        var normalizedName = NormalizeTabName(tabDisplayName ?? tabLegacyId);
        var startIso = fromUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endIso = toUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        using var conn = _factory.OpenConnection();
        var tabIds = conn.Query<long>(
            new CommandDefinition(
                """
                SELECT Id FROM Tabs
                WHERE COALESCE(IsDeleted,0) = 0
                  AND (
                    (@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy)
                    OR (@RoutePk IS NOT NULL AND Id = @RoutePk)
                    OR (@NormalizedName IS NOT NULL AND lower(trim(replace(replace(trim(DisplayName), '  ', ' '), '  ', ' '))) = @NormalizedName)
                  )
                """,
                new { RouteLegacy = routeLegacy, RoutePk = routePk, NormalizedName = normalizedName },
                cancellationToken: cancellationToken)).AsList();

        if (tabIds.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<(long ItemId, int Count)>>(Array.Empty<(long, int)>());
        }

        var counts = new Dictionary<long, int>();
        foreach (var tabId in tabIds)
        {
            var rows = conn.Query<TabDrinkHistoryRow>(
                new CommandDefinition(
                    """
                    SELECT RawJson, Note
                    FROM TabEntries
                    WHERE TabId = @tabId
                      AND EntryType = @drink
                      AND datetime(COALESCE(OccurredAt, CreatedAt)) >= datetime(@startIso)
                      AND datetime(COALESCE(OccurredAt, CreatedAt)) < datetime(@endIso)
                    """,
                    new { tabId, drink = SqliteTabDrinkSalesService.DrinkEntryType, startIso, endIso },
                    cancellationToken: cancellationToken)).AsList();

            foreach (var row in rows)
            {
                if (TryParseItemFromRaw(row.RawJson, out var itemId, out var qty) && itemId > 0)
                {
                    counts[itemId] = counts.GetValueOrDefault(itemId) + Math.Max(1, qty);
                }
            }
        }

        var result = counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();
        return Task.FromResult<IReadOnlyList<(long ItemId, int Count)>>(result);
    }

    private static bool TryParseItemFromRaw(string? rawJson, out long itemId, out int quantity)
    {
        itemId = 0;
        quantity = 1;
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.TryGetProperty("ItemId", out var idEl) && idEl.TryGetInt64(out var id) && id > 0)
            {
                itemId = id;
                if (doc.RootElement.TryGetProperty("Quantity", out var qEl) && qEl.TryGetInt32(out var q) && q > 0)
                {
                    quantity = q;
                }

                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    private static string NormalizeTabName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return name.Replace("  ", " ", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private sealed class TabDrinkHistoryRow
    {
        public string? RawJson { get; set; }

        public string? Note { get; set; }
    }
}