using System.IO.Compression;
using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly TestPaths _paths = new();
    private readonly SqliteConnectionFactory _factory;
    private readonly BackupService _backup;

    public BackupServiceTests()
    {
        _paths.EnsureDirectories();
        _factory = new SqliteConnectionFactory(_paths.DatabasePath);
        var initializer = new DatabaseInitializer(_factory);
        initializer.InitializeAsync().GetAwaiter().GetResult();
        _backup = new BackupService(new AppDatabase(_factory, initializer), _factory, _paths);
    }

    [Fact]
    public async Task Backup_RestoresConsistentDatabase_Config_AndProductImages()
    {
        using (var live = _factory.OpenConnection())
        {
            live.Execute("""
                INSERT INTO Tabs (LegacyId, Name, Balance, IsGuest, TabType, CreatedAt, UpdatedAt)
                VALUES ('tab1', 'Backup test', 42.50, 0, 'Member', datetime('now'), datetime('now'))
                """);
        }
        File.WriteAllText(Path.Combine(_paths.ConfigFolder, "test.json"), "{\"test\":true}");
        Directory.CreateDirectory(Path.Combine(_paths.ImagesFolder, "products"));
        File.WriteAllBytes(Path.Combine(_paths.ImagesFolder, "products", "test.png"), [1, 2, 3]);

        var archive = await _backup.CreateBackupAsync(_paths.BackupsFolder);
        var restored = Path.Combine(_paths.RootFolder, "restored");
        ZipFile.ExtractToDirectory(archive, restored);
        using var snapshot = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(restored, "app.db"), Pooling = false }.ToString());
        snapshot.Open();
        Assert.Equal("ok", snapshot.ExecuteScalar<string>("PRAGMA integrity_check"));
        Assert.Equal(42.50m, snapshot.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE LegacyId = 'tab1'"));
        Assert.Equal("{\"test\":true}", File.ReadAllText(Path.Combine(restored, "config", "test.json")));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(restored, "Images", "products", "test.png")));
        using var liveAfter = _factory.OpenConnection();
        liveAfter.Execute("UPDATE Tabs SET Balance = 100 WHERE LegacyId = 'tab1'");
        Assert.Equal(42.50m, snapshot.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE LegacyId = 'tab1'"));
    }

    [Fact]
    public async Task ConcurrentBackups_DoNotOverwriteEachOther()
    {
        var files = await Task.WhenAll(_backup.CreateBackupAsync(_paths.BackupsFolder), _backup.CreateBackupAsync(_paths.BackupsFolder));
        Assert.NotEqual(files[0], files[1]);
        foreach (var file in files)
        {
            using var zip = ZipFile.OpenRead(file);
            Assert.NotNull(zip.GetEntry("app.db"));
        }
    }

    [Fact]
    public async Task UnreadableCriticalConfig_DoesNotReportSuccessfulPartialBackup()
    {
        var config = Path.Combine(_paths.ConfigFolder, "locked.json");
        using var locked = new FileStream(config, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        await Assert.ThrowsAnyAsync<IOException>(() => _backup.CreateBackupAsync(_paths.BackupsFolder));
        Assert.Empty(Directory.GetFiles(_paths.BackupsFolder, "*.zip"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        var root = Path.GetFullPath(_paths.RootFolder);
        var temp = Path.GetFullPath(Path.GetTempPath());
        if (root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(root).StartsWith("ntpos_backup_test_", StringComparison.Ordinal))
            Directory.Delete(root, recursive: true);
    }

    private sealed class TestPaths : IAppStoragePaths
    {
        public string RootFolder { get; } = Path.Combine(Path.GetTempPath(), "ntpos_backup_test_" + Guid.NewGuid().ToString("N"));
        public string DataFolder => Path.Combine(RootFolder, "Data");
        public string ImagesFolder => Path.Combine(RootFolder, "Images");
        public string ConfigFolder => Path.Combine(RootFolder, "Config");
        public string BackupsFolder => Path.Combine(RootFolder, "Backups");
        public string ReportsFolder => Path.Combine(RootFolder, "Reports");
        public string ImportsFolder => Path.Combine(RootFolder, "Imports");
        public string DatabasePath => Path.Combine(DataFolder, "app.db");
        public string SquareConfigPath => Path.Combine(ConfigFolder, "square_config.json");
        public string MigrationLogsFolder => Path.Combine(DataFolder, "migration-logs");
        public bool IsMsixPackaged => false;
        public bool IsUnderPackageDirectory(string path) => false;
        public bool HasWritableDataBesideExecutable() => false;
        public void EnsureDirectories()
        {
            foreach (var folder in new[] { DataFolder, ImagesFolder, ConfigFolder, BackupsFolder, ReportsFolder, ImportsFolder, MigrationLogsFolder })
                Directory.CreateDirectory(folder);
        }
    }
}
