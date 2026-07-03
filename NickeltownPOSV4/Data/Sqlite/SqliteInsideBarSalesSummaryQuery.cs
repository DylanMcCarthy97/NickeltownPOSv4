using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteInsideBarSalesSummaryQuery : IInsideBarSalesSummaryQuery
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteInsideBarSalesSummaryQuery(SqliteConnectionFactory factory) => _factory = factory;

    public Task<InsideBarSalesSummary> GetSummaryAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var startIso = startInclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endIso = endExclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        var cash = conn.ExecuteScalar<decimal?>(
            new CommandDefinition(
                """
                SELECT COALESCE(SUM(mm.Amount), 0)
                FROM MoneyMovements mm
                WHERE mm.MovementType = 'CashTopUp'
                  AND datetime(COALESCE(mm.OccurredAt, mm.CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(mm.OccurredAt, mm.CreatedAt)) < datetime(@endIso)
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken)) ?? 0m;

        var square = conn.ExecuteScalar<decimal?>(
            new CommandDefinition(
                """
                SELECT COALESCE(SUM(p.Amount), 0)
                FROM Payments p
                WHERE lower(trim(COALESCE(p.Method, ''))) = 'square'
                  AND datetime(p.CreatedAt) >= datetime(@startIso)
                  AND datetime(p.CreatedAt) < datetime(@endIso)
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken)) ?? 0m;

        return Task.FromResult(new InsideBarSalesSummary
        {
            CashTopUps = decimal.Round(cash, 2, MidpointRounding.AwayFromZero),
            SquarePayments = decimal.Round(square, 2, MidpointRounding.AwayFromZero),
        });
    }
}
