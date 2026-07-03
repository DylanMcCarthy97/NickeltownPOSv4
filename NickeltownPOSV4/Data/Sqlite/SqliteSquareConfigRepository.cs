using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteSquareConfigRepository : ISquareConfigMigrationRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteSquareConfigRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task ImportSquareConfigAsync(LegacySquareConfigDto config, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var raw = JsonSerializer.Serialize(config);
        conn.Execute(
            """
            INSERT INTO Settings (Key, Value, IsSecret, UpdatedAt)
            VALUES ('square_config.v2', @Value, 1, datetime('now'))
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedAt = datetime('now')
            """,
            new { Value = raw });
        return Task.CompletedTask;
    }
}
