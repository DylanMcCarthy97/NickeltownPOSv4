using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private bool _durablePragmasApplied;

    /// <summary>Uses <paramref name="databaseFilePath"/> (production: Documents\NickeltownPOS\Data\app.db).</summary>
    public SqliteConnectionFactory(string databaseFilePath)
    {
        if (string.IsNullOrWhiteSpace(databaseFilePath))
        {
            throw new ArgumentException("Database file path is required.", nameof(databaseFilePath));
        }

        var dir = Path.GetDirectoryName(databaseFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        DataSourcePath = databaseFilePath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DataSourcePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public string DataSourcePath { get; }

    public SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        if (!_durablePragmasApplied)
        {
            cmd.CommandText = """
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA temp_store = MEMORY;
                PRAGMA busy_timeout = 5000;
                """;
            cmd.ExecuteNonQuery();
            _durablePragmasApplied = true;
        }
        else
        {
            cmd.CommandText = """
                PRAGMA foreign_keys = ON;
                PRAGMA temp_store = MEMORY;
                PRAGMA busy_timeout = 5000;
                """;
            cmd.ExecuteNonQuery();
        }

        return conn;
    }
}
