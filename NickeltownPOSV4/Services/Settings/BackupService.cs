using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Settings;

public sealed class BackupService : IBackupService
{
    private readonly AppDatabase _database;
    private readonly SqliteConnectionFactory _factory;
    private readonly IAppStoragePaths _paths;

    public BackupService(AppDatabase database, SqliteConnectionFactory factory, IAppStoragePaths paths)
    {
        _database = database;
        _factory = factory;
        _paths = paths;
    }

    public Task<string?> CreateAutomaticBackupAsync(string reason, CancellationToken cancellationToken = default)
    {
        var folder = _paths.BackupsFolder;
        return Task.Run<string?>(
            () =>
            {
                try
                {
                    return CreateBackupCoreAsync(folder, reason, cancellationToken);
                }
                catch
                {
                    return null;
                }
            },
            cancellationToken);
    }

    public Task<string> CreateBackupAsync(string destinationFolder, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationFolder))
        {
            throw new ArgumentException("Destination folder is required.", nameof(destinationFolder));
        }

        return Task.Run(
            () => CreateBackupCoreAsync(destinationFolder, "Manual", cancellationToken),
            cancellationToken);
    }

    private string CreateBackupCoreAsync(string destinationFolder, string reason, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationFolder);
        cancellationToken.ThrowIfCancellationRequested();

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + Guid.NewGuid().ToString("N");
        var stagingFolder = Path.Combine(Path.GetTempPath(), $"NickeltownPOSV4_backup_{timestamp}");
        Directory.CreateDirectory(stagingFolder);

        try
        {
            var stagedDbPath = Path.Combine(stagingFolder, "app.db");
            using (var conn = _factory.OpenConnection())
            {
                var escaped = stagedDbPath.Replace("'", "''");
                conn.Execute($"VACUUM INTO '{escaped}';");
            }

            using (var snapshot = new SqliteConnection(new SqliteConnectionStringBuilder
                   { DataSource = stagedDbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            {
                snapshot.Open();
                var integrity = snapshot.ExecuteScalar<string>("PRAGMA integrity_check;");
                if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Backup database failed its integrity check.");
                }
            }

            CopyConfigSnapshots(stagingFolder, cancellationToken);
            CopyDirectory(_paths.ImagesFolder, Path.Combine(stagingFolder, "Images"), cancellationToken);

            var infoPath = Path.Combine(stagingFolder, "backup_info.txt");
            File.WriteAllText(
                infoPath,
                $"Nickeltown POS v4 backup{Environment.NewLine}"
                    + $"Created: {DateTime.Now:O}{Environment.NewLine}"
                    + $"Reason: {reason}{Environment.NewLine}"
                    + $"Source DB: {_database.DatabaseFilePath}{Environment.NewLine}");

            var zipPath = Path.Combine(destinationFolder, $"NickeltownPOSV4_Backup_{timestamp}.zip");
            ZipFile.CreateFromDirectory(stagingFolder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            return zipPath;
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingFolder))
                {
                    Directory.Delete(stagingFolder, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private void CopyConfigSnapshots(string stagingFolder, CancellationToken cancellationToken)
    {
        var configDir = Path.Combine(stagingFolder, "config");
        Directory.CreateDirectory(configDir);

        // Critical config must be included; do not report a partial archive as a backup.
        CopyDirectory(_paths.ConfigFolder, configDir, cancellationToken);

        try
        {
            if (Directory.Exists(_paths.MigrationLogsFolder))
            {
                var destSub = Path.Combine(configDir, "migration-logs");
                Directory.CreateDirectory(destSub);
                foreach (var file in Directory.EnumerateFiles(_paths.MigrationLogsFolder))
                {
                    File.Copy(file, Path.Combine(destSub, Path.GetFileName(file)), overwrite: true);
                }
            }
        }
        catch
        {
        }

        try
        {
            File.WriteAllText(
                Path.Combine(configDir, "app.db.live-path.txt"),
                _database.DatabaseFilePath);
        }
        catch
        {
        }
    }

    private static void CopyDirectory(string source, string destination, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), cancellationToken);
        }
    }
}
