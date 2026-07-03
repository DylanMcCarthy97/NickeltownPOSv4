using System;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Writes <see cref="DatabaseInitializer"/> MigrationRuns rows for audit and re-import tooling.</summary>
public interface IMigrationRunJournal
{
    void RecordRunStarted(Guid runId, string sourceRoot);

    void RecordRunCompleted(Guid runId, string? notes);
}
