using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteTabHistoryQuery : ITabHistoryQuery
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteTabHistoryQuery(SqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<TabHistoryEntryRow>> GetTabEntriesAsync(string tabLegacyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId)
            || !TabBoardRoute.TryParse(tabLegacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult<IReadOnlyList<TabHistoryEntryRow>>(Array.Empty<TabHistoryEntryRow>());
        }

        using var conn = _factory.OpenConnection();
        var rows = conn.Query<TabHistoryEntryRow>(
            new CommandDefinition(
                """
                SELECT e.OccurredAt, e.CreatedAt, e.EntryType, e.Amount, e.Note, e.RawJson
                FROM TabEntries e
                INNER JOIN Tabs t ON t.Id = e.TabId
                WHERE COALESCE(t.IsDeleted,0) = 0
                  AND ((@RouteLegacy IS NOT NULL AND t.LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND t.Id = @RoutePk))
                ORDER BY datetime(COALESCE(e.OccurredAt, e.CreatedAt)) DESC, e.Id DESC
                """,
                new { RouteLegacy = routeLegacy, RoutePk = routePk },
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<TabHistoryEntryRow>>(rows.AsList());
    }

    public Task<IReadOnlyList<TabHistoryEntryRow>> GetTabEntriesInRangeAsync(
        string tabLegacyId,
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId)
            || !TabBoardRoute.TryParse(tabLegacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult<IReadOnlyList<TabHistoryEntryRow>>(Array.Empty<TabHistoryEntryRow>());
        }

        using var conn = _factory.OpenConnection();
        var startIso = startInclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endIso = endExclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var rows = conn.Query<TabHistoryEntryRow>(
            new CommandDefinition(
                """
                SELECT e.OccurredAt, e.CreatedAt, e.EntryType, e.Amount, e.Note, e.RawJson
                FROM TabEntries e
                INNER JOIN Tabs t ON t.Id = e.TabId
                WHERE COALESCE(t.IsDeleted,0) = 0
                  AND ((@RouteLegacy IS NOT NULL AND t.LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND t.Id = @RoutePk))
                  AND datetime(COALESCE(e.OccurredAt, e.CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(e.OccurredAt, e.CreatedAt)) < datetime(@endIso)
                ORDER BY datetime(COALESCE(e.OccurredAt, e.CreatedAt)) DESC, e.Id DESC
                """,
                new { RouteLegacy = routeLegacy, RoutePk = routePk, startIso, endIso },
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<TabHistoryEntryRow>>(rows.AsList());
    }
}
