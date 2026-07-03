using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
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

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
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

            CopyConfigSnapshots(stagingFolder);

            var infoPath = Path.Combine(stagingFolder, "backup_info.txt");
            File.WriteAllText(
                infoPath,
                $"Nickeltown POS v4 backup{Environment.NewLine}"
                    + $"Created: {DateTime.Now:O}{Environment.NewLine}"
                    + $"Reason: {reason}{Environment.NewLine}"
                    + $"Source DB: {_database.DatabaseFilePath}{Environment.NewLine}");

            var zipPath = Path.Combine(destinationFolder, $"NickeltownPOSV4_Backup_{timestamp}.zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

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

    private void CopyConfigSnapshots(string stagingFolder)
    {
        var configDir = Path.Combine(stagingFolder, "config");
        Directory.CreateDirectory(configDir);

        try
        {
            if (Directory.Exists(_paths.ConfigFolder))
            {
                foreach (var file in Directory.EnumerateFiles(_paths.ConfigFolder))
                {
                    File.Copy(file, Path.Combine(configDir, Path.GetFileName(file)), overwrite: true);
                }
            }
        }
        catch
        {
        }

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
}
