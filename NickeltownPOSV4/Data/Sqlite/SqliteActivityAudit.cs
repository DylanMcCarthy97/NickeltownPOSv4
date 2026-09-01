using System.Data;
using System.Threading;
using Dapper;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Looks up tab/item labels and writes Activity Log rows without blocking POS mutations.</summary>
internal static class SqliteActivityAudit
{
    public const string TabLabelSql =
        "COALESCE(NULLIF(TRIM(DisplayName), ''), NULLIF(TRIM(Name), ''), NULLIF(TRIM(LegacyId), ''), 'tab')";

    public static void TryLog(
        IAuditLogService? audit,
        string actionType,
        string? entityType,
        string? entityId,
        decimal? amount,
        string reason)
    {
        if (audit is null || string.IsNullOrWhiteSpace(actionType) || string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        _ = audit.LogAsync(actionType, entityType, entityId, amount, reason.Trim());
    }

    public static string? LoadTabLabel(
        IDbConnection conn,
        IDbTransaction? tx,
        long tabPk,
        CancellationToken cancellationToken)
    {
        return conn.QuerySingleOrDefault<string>(
            new CommandDefinition(
                $"SELECT {TabLabelSql} FROM Tabs WHERE Id = @id LIMIT 1",
                new { id = tabPk },
                tx,
                cancellationToken: cancellationToken));
    }

    public static string? LoadTabLabelByRoute(
        IDbConnection conn,
        IDbTransaction? tx,
        string? routeLegacy,
        long? routePk,
        CancellationToken cancellationToken)
    {
        return conn.QuerySingleOrDefault<string>(
            new CommandDefinition(
                $"""
                SELECT {TabLabelSql} FROM Tabs
                WHERE ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                LIMIT 1
                """,
                new { RouteLegacy = routeLegacy, RoutePk = routePk },
                tx,
                cancellationToken: cancellationToken));
    }

    public static string? LoadItemName(
        IDbConnection conn,
        IDbTransaction? tx,
        long itemId,
        CancellationToken cancellationToken)
    {
        return conn.QuerySingleOrDefault<string>(
            new CommandDefinition(
                "SELECT COALESCE(NULLIF(TRIM(Name), ''), 'item') FROM Items WHERE Id = @id LIMIT 1",
                new { id = itemId },
                tx,
                cancellationToken: cancellationToken));
    }
}
