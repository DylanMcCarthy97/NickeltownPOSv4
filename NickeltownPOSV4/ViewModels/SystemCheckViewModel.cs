using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels;

public enum SystemCheckSeverity
{
    Ok,
    Warning,
    Error,
}

public sealed class SystemCheckRowVm
{
    public SystemCheckRowVm(string label, string value, SystemCheckSeverity severity, string? hint = null)
    {
        Label = label;
        Value = value;
        Severity = severity;
        Hint = hint ?? string.Empty;
    }

    public string Label { get; }

    public string Value { get; }

    public SystemCheckSeverity Severity { get; }

    public string Hint { get; }

    public string SeverityGlyph => Severity switch
    {
        SystemCheckSeverity.Ok => "\uE73E",      // checkmark
        SystemCheckSeverity.Warning => "\uE7BA", // warning triangle
        SystemCheckSeverity.Error => "\uE783",   // error circle
        _ => "\uE946",
    };

    public string SeverityText => Severity switch
    {
        SystemCheckSeverity.Ok => "OK",
        SystemCheckSeverity.Warning => "Warning",
        SystemCheckSeverity.Error => "Error",
        _ => "Info",
    };

    public string SeverityBrushKey => Severity switch
    {
        SystemCheckSeverity.Ok => "PosSuccessBrush",
        SystemCheckSeverity.Warning => "PosWarningAmberBrush",
        SystemCheckSeverity.Error => "PosButtonDangerBrush",
        _ => "PosTextSecondaryBrush",
    };
}

public sealed class SystemCheckViewModel : ObservableViewModel
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    private readonly IAppStoragePaths _storagePaths;
    private readonly AppDatabase _database;
    private readonly SqliteConnectionFactory _factory;
    private readonly IBackupService _backupService;
    private readonly ISquareConfigService _squareConfig;
    private readonly ISquareRecoveryRepository _recovery;
    private readonly IPitstopEodBatchRepository _pitstopBatches;
    private readonly IUserSessionService _session;
    private readonly INavigationService _navigation;

    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public SystemCheckViewModel(
        IAppStoragePaths storagePaths,
        AppDatabase database,
        SqliteConnectionFactory factory,
        IBackupService backupService,
        ISquareConfigService squareConfig,
        ISquareRecoveryRepository recovery,
        IPitstopEodBatchRepository pitstopBatches,
        IUserSessionService session,
        INavigationService navigation)
    {
        _storagePaths = storagePaths;
        _database = database;
        _factory = factory;
        _backupService = backupService;
        _squareConfig = squareConfig;
        _recovery = recovery;
        _pitstopBatches = pitstopBatches;
        _session = session;
        _navigation = navigation;
        Rows = new ObservableCollection<SystemCheckRowVm>();
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        RunBackupCommand = new AsyncRelayCommand(RunBackupAsync);
        BackCommand = new RelayCommand(() => _navigation.TryGoBack());
    }

    public ObservableCollection<SystemCheckRowVm> Rows { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand RunBackupCommand { get; }

    public IRelayCommand BackCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task InitializeAsync()
    {
        if (!_session.CanAccessTreasurer)
        {
            StatusMessage = "Admin/Treasurer access required.";
            _navigation.TryGoBack();
            return;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    public async Task LoadAsync()
    {
        if (!_session.CanAccessTreasurer)
        {
            return;
        }

        try
        {
            IsBusy = true;
            Rows.Clear();
            await CollectRowsAsync().ConfigureAwait(true);
            StatusMessage = $"Refreshed at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"System Check failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CollectRowsAsync()
    {
        AddStorageLocationRows();

        // 1. Database
        var dbPath = _database.DatabaseFilePath;
        var dbExists = File.Exists(dbPath);
        var dbSizeMb = dbExists ? new FileInfo(dbPath).Length / 1024.0 / 1024.0 : 0;
        Rows.Add(new SystemCheckRowVm(
            "Database file",
            dbExists ? $"{dbPath} ({dbSizeMb:F2} MB)" : $"Missing: {dbPath}",
            dbExists ? SystemCheckSeverity.Ok : SystemCheckSeverity.Error,
            dbExists ? "SQLite database is present." : "POS will not run without the database file."));

        try
        {
            using var conn = _factory.OpenConnection();
            var staffCount = conn.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM Bartenders WHERE COALESCE(IsActive,1)!=0");
            var itemCount = conn.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM Items WHERE COALESCE(IsActive,1)!=0");

            Rows.Add(new SystemCheckRowVm(
                "Active staff",
                staffCount.ToString(Cult),
                staffCount > 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                staffCount > 0 ? null : "No active Bartender rows — sign-in will fail."));

            Rows.Add(new SystemCheckRowVm(
                "Stock items",
                itemCount.ToString(Cult),
                itemCount > 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                itemCount > 0 ? null : "No active Items configured."));
        }
        catch (Exception ex)
        {
            Rows.Add(new SystemCheckRowVm(
                "Database connection",
                ex.Message,
                SystemCheckSeverity.Error,
                "Could not query Bartenders/Items."));
        }

        // 2. Backups
        var backupFolder = _storagePaths.BackupsFolder;
        try
        {
            Directory.CreateDirectory(backupFolder);
            var writable = TestFolderWritable(backupFolder);

            Rows.Add(new SystemCheckRowVm(
                "Backup folder writable",
                writable ? "Yes" : "No",
                writable ? SystemCheckSeverity.Ok : SystemCheckSeverity.Error,
                backupFolder));

            var latest = Directory.EnumerateFiles(backupFolder, "NickeltownPOSV4_Backup_*.zip")
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (latest is null)
            {
                Rows.Add(new SystemCheckRowVm(
                    "Last backup",
                    "No backup found yet.",
                    SystemCheckSeverity.Warning,
                    "Run Backup now from Admin or trigger a Pitstop archive."));
            }
            else
            {
                var ageHours = (DateTime.UtcNow - latest.LastWriteTimeUtc).TotalHours;
                var severity = ageHours > 48 ? SystemCheckSeverity.Warning : SystemCheckSeverity.Ok;
                Rows.Add(new SystemCheckRowVm(
                    "Last backup",
                    $"{latest.LastWriteTime:yyyy-MM-dd HH:mm} ({latest.Name})",
                    severity,
                    severity == SystemCheckSeverity.Warning
                        ? "Backup is older than 48 hours."
                        : "Backup is recent."));
            }
        }
        catch (Exception ex)
        {
            Rows.Add(new SystemCheckRowVm(
                "Backup folder",
                ex.Message,
                SystemCheckSeverity.Error,
                "Backup directory is not accessible."));
        }

        // 3. Square config file + SQLite settings
        var squareFileExists = File.Exists(_storagePaths.SquareConfigPath);
        Rows.Add(new SystemCheckRowVm(
            "square_config.json (Config folder)",
            squareFileExists ? _storagePaths.SquareConfigPath : "Not found",
            squareFileExists ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
            squareFileExists
                ? "Legacy JSON copy on disk (runtime config is also stored in SQLite)."
                : "Optional file copy; Square settings may still be in the database."));

        try
        {
            var cfg = await _squareConfig.LoadAsync().ConfigureAwait(true);
            var env = string.IsNullOrWhiteSpace(cfg.Environment) ? "(unset)" : cfg.Environment;
            Rows.Add(new SystemCheckRowVm(
                "Square environment",
                env,
                string.Equals(env, "production", StringComparison.OrdinalIgnoreCase)
                    ? SystemCheckSeverity.Ok
                    : SystemCheckSeverity.Warning,
                string.Equals(env, "sandbox", StringComparison.OrdinalIgnoreCase)
                    ? "Sandbox keys in use — card payments are not real."
                    : null));

            Rows.Add(new SystemCheckRowVm(
                "Square Location Id",
                string.IsNullOrWhiteSpace(cfg.LocationId) ? "(missing)" : MaskSecret(cfg.LocationId),
                string.IsNullOrWhiteSpace(cfg.LocationId) ? SystemCheckSeverity.Error : SystemCheckSeverity.Ok,
                string.IsNullOrWhiteSpace(cfg.LocationId) ? "Card payments cannot work without a Location Id." : null));

            Rows.Add(new SystemCheckRowVm(
                "Square Device Id",
                string.IsNullOrWhiteSpace(cfg.DeviceId) ? "(missing)" : MaskSecret(cfg.DeviceId),
                string.IsNullOrWhiteSpace(cfg.DeviceId) ? SystemCheckSeverity.Error : SystemCheckSeverity.Ok,
                string.IsNullOrWhiteSpace(cfg.DeviceId) ? "Terminal card payments cannot start without a Device Id." : null));
        }
        catch (Exception ex)
        {
            Rows.Add(new SystemCheckRowVm(
                "Square config",
                ex.Message,
                SystemCheckSeverity.Error,
                "Could not read Square config."));
        }

        // 4. Pitstop status
        try
        {
            var active = await _pitstopBatches.GetActivePitstopSaleCountAsync().ConfigureAwait(true);
            Rows.Add(new SystemCheckRowVm(
                "Active Pitstop-area sales",
                active.ToString(Cult),
                active == 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                active == 0
                    ? "No unarchived Pitstop-area sales — last event has been archived. Tab-area sales are tracked separately and never counted here."
                    : "There are unarchived Pitstop-area sales. Run Pitstop EOD to archive. Tab-area sales are tracked separately and not included in this count."));

            var nonPitstopRows = await _pitstopBatches.GetNonPitstopSaleModeCountAsync().ConfigureAwait(true);
            Rows.Add(new SystemCheckRowVm(
                "Pitstop sales scope",
                nonPitstopRows == 0
                    ? "Pitstop-area only"
                    : $"{nonPitstopRows.ToString(Cult)} non-Pitstop row(s) found",
                nonPitstopRows == 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                nonPitstopRows == 0
                    ? "Verified: every tagged row in PitstopSales is from the Pitstop area (SaleMode='Pitstop'). Tab-area sales live in the Tabs/TabEntries/Payments tables and are not mixed in."
                    : "Found rows in PitstopSales whose SaleMode is set to something other than 'Pitstop'. These are not counted as Pitstop sales, but please report this — Pitstop-area and Tab-area sales should never share the same table."));

            var batches = await _pitstopBatches.ListBatchesAsync().ConfigureAwait(true);
            var lastBatch = batches.FirstOrDefault();
            Rows.Add(new SystemCheckRowVm(
                "Last EOD archive",
                lastBatch is null
                    ? "(never)"
                    : $"{lastBatch.ArchivedAt.LocalDateTime:yyyy-MM-dd HH:mm} — {lastBatch.OperatorName ?? "—"}",
                lastBatch is null ? SystemCheckSeverity.Warning : SystemCheckSeverity.Ok));
        }
        catch (Exception ex)
        {
            Rows.Add(new SystemCheckRowVm(
                "Pitstop status",
                ex.Message,
                SystemCheckSeverity.Error));
        }

        // 5. Square recovery
        try
        {
            var unresolved = await _recovery.GetUnresolvedCountAsync().ConfigureAwait(true);
            Rows.Add(new SystemCheckRowVm(
                "Unresolved Square payments",
                unresolved.ToString(Cult),
                unresolved == 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                unresolved == 0
                    ? "No orphan Square payments to reconcile."
                    : "Open Square Recovery to link or reconcile these payments."));
        }
        catch (Exception ex)
        {
            Rows.Add(new SystemCheckRowVm(
                "Square recovery",
                ex.Message,
                SystemCheckSeverity.Error));
        }

        // 6. App version
        var version = typeof(SystemCheckViewModel).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        Rows.Add(new SystemCheckRowVm(
            "App version",
            version,
            SystemCheckSeverity.Ok));
    }

    private async Task RunBackupAsync()
    {
        if (!_session.CanAccessTreasurer)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Creating backup...";
            var zipPath = await _backupService.CreateAutomaticBackupAsync("Manual System Check").ConfigureAwait(true);
            StatusMessage = string.IsNullOrEmpty(zipPath)
                ? "Backup failed."
                : $"Backup created: {zipPath}";
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddStorageLocationRows()
    {
        _storagePaths.EnsureDirectories();

        Rows.Add(new SystemCheckRowVm(
            "Data root (Documents)",
            _storagePaths.RootFolder,
            SystemCheckSeverity.Ok,
            "Permanent POS data lives here — MSIX updates do not touch this folder."));

        Rows.Add(new SystemCheckRowVm(
            "Database path",
            _storagePaths.DatabasePath,
            File.Exists(_storagePaths.DatabasePath) ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
            File.Exists(_storagePaths.DatabasePath) ? "Database file found." : "Database will be created on first run."));

        AddFolderRow("Images folder", _storagePaths.ImagesFolder);
        AddFolderRow("Config folder", _storagePaths.ConfigFolder);
        AddFolderRow("Backup folder", _storagePaths.BackupsFolder);
        AddFolderRow("Reports folder", _storagePaths.ReportsFolder);
        AddFolderRow("Imports folder", _storagePaths.ImportsFolder);

        if (_storagePaths.IsMsixPackaged)
        {
            Rows.Add(new SystemCheckRowVm(
                "MSIX packaged app",
                "Yes — install folder is read-only",
                SystemCheckSeverity.Ok,
                $"Package install dir: {AppContext.BaseDirectory}"));

            if (_storagePaths.HasWritableDataBesideExecutable())
            {
                Rows.Add(new SystemCheckRowVm(
                    "Data beside install folder",
                    "Detected — move to Documents\\NickeltownPOS",
                    SystemCheckSeverity.Warning,
                    "Database or config files were found next to the MSIX app. They will not survive updates reliably."));
            }
        }
        else
        {
            Rows.Add(new SystemCheckRowVm(
                "MSIX packaged app",
                "No (debug / unpackaged)",
                SystemCheckSeverity.Ok,
                "Writable data still uses Documents\\NickeltownPOS for consistency."));
        }
    }

    private void AddFolderRow(string label, string folder)
    {
        var writable = TestFolderWritable(folder);
        Rows.Add(new SystemCheckRowVm(
            label,
            folder,
            writable ? SystemCheckSeverity.Ok : SystemCheckSeverity.Error,
            writable ? "Exists and is writable." : "Cannot write to this folder."));
    }

    private static bool TestFolderWritable(string folder)
    {
        var probe = Path.Combine(folder, $".write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length <= 4)
        {
            return new string('•', value.Length);
        }

        return string.Concat(new string('•', value.Length - 4), value[^4..]);
    }
}
