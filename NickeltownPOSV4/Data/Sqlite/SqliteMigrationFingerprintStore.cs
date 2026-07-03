using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteMigrationFingerprintStore : IMigrationFingerprintStore
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteMigrationFingerprintStore(SqliteConnectionFactory factory) => _factory = factory;

    public Task<bool> WasSuccessfullyImportedAsync(
        LegacyJsonFileKind kind,
        string sourcePath,
        string contentSha256Hex,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var n = conn.ExecuteScalar<long>(
            """
            SELECT COUNT(1) FROM MigrationImportedFiles
            WHERE FileKind = @k AND SourcePath = @p AND ContentSha256 = @h
            """,
            new { k = (int)kind, p = sourcePath, h = contentSha256Hex });
        return Task.FromResult(n > 0);
    }

    public Task MarkSuccessfullyImportedAsync(
        LegacyJsonFileKind kind,
        string sourcePath,
        string contentSha256Hex,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(
            """
            INSERT OR IGNORE INTO MigrationImportedFiles (FileKind, SourcePath, ContentSha256, ImportedAtUtc)
            VALUES (@k, @p, @h, datetime('now'))
            """,
            new { k = (int)kind, p = sourcePath, h = contentSha256Hex });
        return Task.CompletedTask;
    }
}
