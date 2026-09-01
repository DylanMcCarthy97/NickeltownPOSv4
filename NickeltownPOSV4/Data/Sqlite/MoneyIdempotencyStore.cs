using System.Threading;
using Dapper;
using Microsoft.Data.Sqlite;

namespace NickeltownPOSV4.Data.Sqlite;

internal static class MoneyIdempotencyStore
{
    public const string KindFundMovement = "FundMovement";
    public const string KindDrinkSale = "DrinkSale";
    public const string KindComplimentaryItem = "ComplimentaryItem";

    public readonly record struct ClaimResult(bool AlreadyExists, string? ResultRef);

    public static ClaimResult TryClaim(
        SqliteConnection conn,
        SqliteTransaction tx,
        string idempotencyKey,
        string operationKind,
        CancellationToken cancellationToken)
    {
        var key = idempotencyKey.Trim();
        conn.Execute(
            new CommandDefinition(
                """
                INSERT OR IGNORE INTO MoneyIdempotencyKeys (IdempotencyKey, OperationKind, CreatedAt)
                VALUES (@key, @kind, datetime('now'))
                """,
                new { key, kind = operationKind },
                tx,
                cancellationToken: cancellationToken));

        var claimed = conn.ExecuteScalar<long>(
            new CommandDefinition(
                "SELECT changes();",
                transaction: tx,
                cancellationToken: cancellationToken));

        if (claimed > 0)
        {
            return new ClaimResult(false, null);
        }

        var existing = conn.ExecuteScalar<string?>(
            new CommandDefinition(
                "SELECT ResultRef FROM MoneyIdempotencyKeys WHERE IdempotencyKey = @key LIMIT 1",
                new { key },
                tx,
                cancellationToken: cancellationToken));

        return new ClaimResult(true, string.IsNullOrWhiteSpace(existing) ? key : existing);
    }

    public static void SetResultRef(
        SqliteConnection conn,
        SqliteTransaction tx,
        string idempotencyKey,
        string resultRef,
        CancellationToken cancellationToken)
    {
        conn.Execute(
            new CommandDefinition(
                """
                UPDATE MoneyIdempotencyKeys
                SET ResultRef = @resultRef
                WHERE IdempotencyKey = @key
                """,
                new { key = idempotencyKey.Trim(), resultRef },
                tx,
                cancellationToken: cancellationToken));
    }
}
