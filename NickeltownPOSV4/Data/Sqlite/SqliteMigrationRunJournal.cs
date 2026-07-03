using System;
using Dapper;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteMigrationRunJournal : IMigrationRunJournal
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteMigrationRunJournal(SqliteConnectionFactory factory) => _factory = factory;

    public void RecordRunStarted(Guid runId, string sourceRoot)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(
            """
            INSERT INTO MigrationRuns (RunId, SourceRoot, StartedAtUtc, CompletedAtUtc, Notes)
            VALUES (@RunId, @SourceRoot, datetime('now'), NULL, NULL)
            ON CONFLICT(RunId) DO UPDATE SET
              SourceRoot = excluded.SourceRoot,
              StartedAtUtc = excluded.StartedAtUtc,
              CompletedAtUtc = NULL,
              Notes = NULL
            """,
            new { RunId = runId.ToString("N"), SourceRoot = sourceRoot });
    }

    public void RecordRunCompleted(Guid runId, string? notes)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(
            """
            UPDATE MigrationRuns
            SET CompletedAtUtc = datetime('now'), Notes = @Notes
            WHERE RunId = @RunId
            """,
            new { RunId = runId.ToString("N"), Notes = notes });
    }
}
