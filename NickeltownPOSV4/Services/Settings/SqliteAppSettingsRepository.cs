using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.Settings;

public sealed class SqliteAppSettingsRepository : IAppSettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger<SqliteAppSettingsRepository> _logger;

    public SqliteAppSettingsRepository(SqliteConnectionFactory factory, ILogger<SqliteAppSettingsRepository> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var conn = _factory.OpenConnection();
        var row = conn.QuerySingleOrDefault<SettingsRow>(
            "SELECT Value, IsSecret FROM Settings WHERE Key = @Key",
            new { Key = key });
        if (row is null || string.IsNullOrWhiteSpace(row.Value))
        {
            return Task.FromResult<T?>(null);
        }

        var json = ResolveStoredJson(row.Value, row.IsSecret == 1);
        if (json is null)
        {
            _logger.LogWarning("Could not read protected setting {Key}.", key);
            return Task.FromResult<T?>(null);
        }

        try
        {
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, JsonOptions));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Settings JSON invalid for key {Key}.", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, bool isSecret = false, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = JsonSerializer.Serialize(value, JsonOptions);
        if (isSecret)
        {
            raw = SettingsSecretProtector.Protect(raw);
        }

        using var conn = _factory.OpenConnection();
        conn.Execute(
            """
            INSERT INTO Settings (Key, Value, IsSecret, UpdatedAt)
            VALUES (@Key, @Value, @IsSecret, datetime('now'))
            ON CONFLICT(Key) DO UPDATE SET
              Value = excluded.Value,
              IsSecret = excluded.IsSecret,
              UpdatedAt = datetime('now')
            """,
            new { Key = key, Value = raw, IsSecret = isSecret ? 1 : 0 });
        return Task.CompletedTask;
    }

    private static string? ResolveStoredJson(string stored, bool isSecret)
    {
        if (!isSecret)
        {
            return stored;
        }

        if (SettingsSecretProtector.TryUnprotect(stored, out var json))
        {
            return json;
        }

        return null;
    }

    private sealed class SettingsRow
    {
        public string? Value { get; set; }

        public int IsSecret { get; set; }
    }
}
