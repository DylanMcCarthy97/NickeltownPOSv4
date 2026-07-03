using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Services.TabFavorites;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteTabBarFavoritesService : ITabBarFavoritesService
{
    private readonly SqliteConnectionFactory _factory;

    private readonly ITabBarFavoritesHistoryQuery _history;

    public SqliteTabBarFavoritesService(SqliteConnectionFactory factory, ITabBarFavoritesHistoryQuery history)
    {
        _factory = factory;
        _history = history;
    }

    public Task<IReadOnlyList<long>> GetManualFavoriteItemIdsOrderedAsync(
        string tabLegacyId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId))
        {
            return Task.FromResult<IReadOnlyList<long>>(new List<long>());
        }

        using var conn = _factory.OpenConnection();
        var rows = conn.Query<long>(
            new CommandDefinition(
                """
                SELECT ItemId
                FROM TabBarFavoriteItems
                WHERE TabLegacyId = @TabLegacyId
                ORDER BY SortOrder ASC, ItemId ASC
                """,
                new { TabLegacyId = tabLegacyId.Trim() },
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<long>>(rows.AsList());
    }

    public async Task<IReadOnlyList<long>> GetEffectiveFavoriteItemIdsOrderedAsync(
        string tabLegacyId,
        string? tabDisplayName,
        IReadOnlyDictionary<long, int> sessionCounts,
        CancellationToken cancellationToken = default)
    {
        var manual = await GetManualFavoriteItemIdsOrderedAsync(tabLegacyId, cancellationToken).ConfigureAwait(false);
        var toUtc = DateTimeOffset.UtcNow;
        var fromUtc = new DateTimeOffset(toUtc.Year, toUtc.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var history = await _history
            .GetFavoriteItemCountsForTabAsync(tabLegacyId, tabDisplayName, fromUtc, toUtc, cancellationToken)
            .ConfigureAwait(false);
        return TabFavoritesCalculator.GetEffectiveFavoriteItemIds(history, sessionCounts, manual);
    }

    public Task SetFavoriteAsync(
        string tabLegacyId,
        long itemId,
        bool favorite,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId) || itemId <= 0)
        {
            return Task.CompletedTask;
        }

        var tab = tabLegacyId.Trim();
        using var conn = _factory.OpenConnection();
        if (!favorite)
        {
            conn.Execute(
                new CommandDefinition(
                    "DELETE FROM TabBarFavoriteItems WHERE TabLegacyId = @TabLegacyId AND ItemId = @ItemId",
                    new { TabLegacyId = tab, ItemId = itemId },
                    cancellationToken: cancellationToken));
            return Task.CompletedTask;
        }

        var exists = conn.ExecuteScalar<long>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM TabBarFavoriteItems WHERE TabLegacyId = @TabLegacyId AND ItemId = @ItemId",
                new { TabLegacyId = tab, ItemId = itemId },
                cancellationToken: cancellationToken));
        if (exists > 0)
        {
            return Task.CompletedTask;
        }

        var nextOrder = conn.ExecuteScalar<long?>(
            new CommandDefinition(
                "SELECT MAX(SortOrder) FROM TabBarFavoriteItems WHERE TabLegacyId = @TabLegacyId",
                new { TabLegacyId = tab },
                cancellationToken: cancellationToken)) ?? -1;
        var stamp = System.DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO TabBarFavoriteItems (TabLegacyId, ItemId, SortOrder, CreatedAt)
                VALUES (@TabLegacyId, @ItemId, @SortOrder, @CreatedAt)
                """,
                new
                {
                    TabLegacyId = tab,
                    ItemId = itemId,
                    SortOrder = nextOrder + 1,
                    CreatedAt = stamp,
                },
                cancellationToken: cancellationToken));
        return Task.CompletedTask;
    }
}
