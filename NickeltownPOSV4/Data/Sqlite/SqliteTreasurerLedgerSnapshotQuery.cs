using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NickeltownPOSV4.Data.Sqlite;

public interface ITreasurerLedgerSnapshotQuery
{
    Task<(int MoneyMovementCount, decimal MoneyMovementNet)> GetTodayAsync(CancellationToken cancellationToken = default);
}

public sealed class SqliteTreasurerLedgerSnapshotQuery : ITreasurerLedgerSnapshotQuery
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteTreasurerLedgerSnapshotQuery(SqliteConnectionFactory factory) => _factory = factory;

    public Task<(int MoneyMovementCount, decimal MoneyMovementNet)> GetTodayAsync(
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM MoneyMovements
                WHERE date(COALESCE(OccurredAt, CreatedAt)) = date('now')
                """,
                cancellationToken: cancellationToken));
        var net = conn.ExecuteScalar<double?>(
            new CommandDefinition(
                """
                SELECT SUM(Amount)
                FROM MoneyMovements
                WHERE date(COALESCE(OccurredAt, CreatedAt)) = date('now')
                """,
                cancellationToken: cancellationToken));
        var netDec = net.HasValue ? (decimal)net.Value : 0m;
        return Task.FromResult(((int)count, decimal.Round(netDec, 2, System.MidpointRounding.AwayFromZero)));
    }
}
