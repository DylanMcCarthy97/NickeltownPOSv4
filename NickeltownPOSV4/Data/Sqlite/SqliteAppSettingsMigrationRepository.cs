using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Migration;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteAppSettingsMigrationRepository : IAppSettingsMigrationRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteAppSettingsMigrationRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task ImportSettingsDocumentAsync(string relativePath, JsonDocument document, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var key = "settings.v2:" + relativePath.Replace('\\', '/');
        var raw = document.RootElement.GetRawText();
        var isSecret = relativePath.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("token", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("password", StringComparison.OrdinalIgnoreCase);

        conn.Execute(
            """
            INSERT INTO Settings (Key, Value, IsSecret, UpdatedAt)
            VALUES (@Key, @Value, @IsSecret, datetime('now'))
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, IsSecret = excluded.IsSecret, UpdatedAt = datetime('now')
            """,
            new { Key = key, Value = raw, IsSecret = isSecret ? 1 : 0 });
        return Task.CompletedTask;
    }
}
