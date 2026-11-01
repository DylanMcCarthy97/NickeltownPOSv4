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
            StatusMessage = "A manager needs to open this screen.";
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
            StatusMessage = PosUserMessage.FromException(ex, "We couldn't check the till. Try Refresh.");
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
            "Till data",
            dbExists ? $"Found ({dbSizeMb:F2} MB)" : "Missing",
            dbExists ? SystemCheckSeverity.Ok : SystemCheckSeverity.Error,
            dbExists ? "The till data file is there." : "The till cannot run without its data file. Tell a manager."));

        try
        {
            using var conn = _factory.OpenConnection();
            var staffCount = conn.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM Bartenders WHERE COALESCE(IsActive,1)!=0");
            var itemCount = conn.ExecuteScalar<long>(
                "SELECT COUNT(1) FROM Items WHERE COALESCE(IsActive,1)!=0");

            Rows.Add(new SystemCheckRowVm(
                "Staff who can sign in",
                staffCount.ToString(Cult),
                staffCount > 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                staffCount > 0 ? null : "Nobody can sign in. Add a staff member in Admin → Staff."));

            Rows.Add(new SystemCheckRowVm(
                "Products",
                itemCount.ToString(Cult),
                itemCount > 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                itemCount > 0 ? null : "No products are set up. Open Admin → Stock."));
        }
        catch (Exception)
        {
            Rows.Add(new SystemCheckRowVm(
                "Till data",
                "Cannot read",
                SystemCheckSeverity.Error,
                "We couldn't read staff or products. Restart the till and tell a manager if it keeps happening."));
        }

        // 2. Backups
        var backupFolder = _storagePaths.BackupsFolder;
        try
        {
            Directory.CreateDirectory(backupFolder);
            var writable = TestFolderWritable(backupFolder);

            Rows.Add(new SystemCheckRowVm(
                "Can save backups",
                writable ? "Yes" : "No",
                writable ? SystemCheckSeverity.Ok : SystemCheckSeverity.Error,
                writable ? "Backup folder is ready." : "Cannot write backups. Check the backup folder."));

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
                    "Make a backup from Admin, or after a Pitstop close."));
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
        catch (Exception)
        {
            Rows.Add(new SystemCheckRowVm(
                "Backup folder",
                "Cannot open",
                SystemCheckSeverity.Error,
                "We couldn't reach the backup folder."));
        }

        // 3. Square config file + SQLite settings
        var squareFileExists = File.Exists(_storagePaths.SquareConfigPath);
        Rows.Add(new SystemCheckRowVm(
            "Saved card settings file",
            squareFileExists ? "Found" : "Not found",
            squareFileExists ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
            squareFileExists
                ? "A backup copy of card settings is on disk."
                : "Optional file. Card settings may still be saved in the till."));

        try
        {
            var cfg = await _squareConfig.LoadAsync().ConfigureAwait(true);
            var env = string.IsNullOrWhiteSpace(cfg.Environment) ? "(unset)" : cfg.Environment;
            Rows.Add(new SystemCheckRowVm(
                "Card reader mode",
                env,
                string.Equals(env, "production", StringComparison.OrdinalIgnoreCase)
                    ? SystemCheckSeverity.Ok
                    : SystemCheckSeverity.Warning,
                string.Equals(env, "sandbox", StringComparison.OrdinalIgnoreCase)
                    ? "Test mode is on — card payments are not real."
                    : null));

            Rows.Add(new SystemCheckRowVm(
                "Square shop",
                string.IsNullOrWhiteSpace(cfg.LocationId) ? "Not set" : "Set",
                string.IsNullOrWhiteSpace(cfg.LocationId) ? SystemCheckSeverity.Error : SystemCheckSeverity.Ok,
                string.IsNullOrWhiteSpace(cfg.LocationId) ? "Card payments need a Square shop. Open Admin → Setup → Card reader." : null));

            Rows.Add(new SystemCheckRowVm(
                "Card reader",
                string.IsNullOrWhiteSpace(cfg.DeviceId) ? "Not set" : "Set",
                string.IsNullOrWhiteSpace(cfg.DeviceId) ? SystemCheckSeverity.Error : SystemCheckSeverity.Ok,
                string.IsNullOrWhiteSpace(cfg.DeviceId) ? "The card reader is not set up. Open Admin → Setup → Card reader." : null));
        }
        catch (Exception)
        {
            Rows.Add(new SystemCheckRowVm(
                "Card reader",
                "Cannot read",
                SystemCheckSeverity.Error,
                "We couldn't read card settings. Open Admin → Setup → Card reader."));
        }

        // 4. Pitstop status
        try
        {
            var active = await _pitstopBatches.GetActivePitstopSaleCountAsync().ConfigureAwait(true);
            Rows.Add(new SystemCheckRowVm(
                "Open Pitstop sales",
                active.ToString(Cult),
                active == 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                active == 0
                    ? "Last Pitstop day has been closed."
                    : "There are sales from the current Pitstop day. Close Pitstop when you are done."));

            var nonPitstopRows = await _pitstopBatches.GetNonPitstopSaleModeCountAsync().ConfigureAwait(true);
            Rows.Add(new SystemCheckRowVm(
                "Pitstop sales look right",
                nonPitstopRows == 0
                    ? "Yes"
                    : $"{nonPitstopRows.ToString(Cult)} odd row(s)",
                nonPitstopRows == 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                nonPitstopRows == 0
                    ? "Pitstop sales are only Pitstop sales."
                    : "Some sales in Pitstop look like they belong somewhere else. Tell a manager."));

            var batches = await _pitstopBatches.ListBatchesAsync().ConfigureAwait(true);
            var lastBatch = batches.FirstOrDefault();
            Rows.Add(new SystemCheckRowVm(
                "Last Pitstop close",
                lastBatch is null
                    ? "Never"
                    : $"{lastBatch.ArchivedAt.LocalDateTime:yyyy-MM-dd HH:mm} — {lastBatch.OperatorName ?? "—"}",
                lastBatch is null ? SystemCheckSeverity.Warning : SystemCheckSeverity.Ok));
        }
        catch (Exception)
        {
            Rows.Add(new SystemCheckRowVm(
                "Pitstop",
                "Cannot read",
                SystemCheckSeverity.Error,
                "We couldn't check Pitstop. Try Refresh."));
        }

        // 5. Square recovery
        try
        {
            var unresolved = await _recovery.GetUnresolvedCountAsync().ConfigureAwait(true);
            Rows.Add(new SystemCheckRowVm(
                "Card payments to fix",
                unresolved.ToString(Cult),
                unresolved == 0 ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
                unresolved == 0
                    ? "No unfinished card payments."
                    : "Open Admin → Setup → Fix payments. Don't charge those cards again until they are fixed."));
        }
        catch (Exception)
        {
            Rows.Add(new SystemCheckRowVm(
                "Card payments to fix",
                "Cannot read",
                SystemCheckSeverity.Error,
                "We couldn't check unfinished payments."));
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
                ? "Backup didn't finish. Try again."
                : "Backup saved.";
            await LoadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = PosUserMessage.FromException(ex, "Backup didn't finish. Try again.");
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
            "Till folder",
            "Documents folder",
            SystemCheckSeverity.Ok,
            "Till files stay here when the app is updated."));

        Rows.Add(new SystemCheckRowVm(
            "Till data file",
            File.Exists(_storagePaths.DatabasePath) ? "Found" : "Not created yet",
            File.Exists(_storagePaths.DatabasePath) ? SystemCheckSeverity.Ok : SystemCheckSeverity.Warning,
            File.Exists(_storagePaths.DatabasePath) ? "The till data file is there." : "A new data file will be created the first time the till runs."));

        AddFolderRow("Pictures folder", _storagePaths.ImagesFolder);
        AddFolderRow("Settings folder", _storagePaths.ConfigFolder);
        AddFolderRow("Backup folder", _storagePaths.BackupsFolder);
        AddFolderRow("Reports folder", _storagePaths.ReportsFolder);
        AddFolderRow("Import folder", _storagePaths.ImportsFolder);

        if (_storagePaths.IsMsixPackaged)
        {
            Rows.Add(new SystemCheckRowVm(
                "Installed app",
                "Yes",
                SystemCheckSeverity.Ok,
                "This till is running the installed app."));

            if (_storagePaths.HasWritableDataBesideExecutable())
            {
                Rows.Add(new SystemCheckRowVm(
                    "Old data next to the app",
                    "Found",
                    SystemCheckSeverity.Warning,
                    "Some till files are next to the app and may be lost on update. Move them to Documents\\NickeltownPOS."));
            }
        }
        else
        {
            Rows.Add(new SystemCheckRowVm(
                "Installed app",
                "No — test copy",
                SystemCheckSeverity.Ok,
                "This is a test copy. Live tills should use the installed app."));
        }
    }

    private void AddFolderRow(string label, string folder)
    {
        var writable = TestFolderWritable(folder);
        Rows.Add(new SystemCheckRowVm(
            label,
            writable ? "Ready" : "Cannot write",
            writable ? SystemCheckSeverity.Ok : SystemCheckSeverity.Error,
            writable ? "This folder is ready." : "Cannot write to this folder."));
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

}
