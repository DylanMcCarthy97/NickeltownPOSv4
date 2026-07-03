using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Services.Treasurer;

namespace NickeltownPOSV4.Services.Settings;

/// <summary>Saturday 6 AM backup, Google Drive upload, and weekly stock email (POSBar V2 BackupManager).</summary>
public sealed class ScheduledMaintenanceService : IDisposable
{
    private readonly IBackupService _backup;
    private readonly IReportExportService _reports;
    private readonly IEmailSender _email;
    private readonly GoogleDriveBackupUploader _drive;
    private readonly object _gate = new();
    private Timer? _backupTimer;
    private Timer? _dailyCheckTimer;
    private bool _started;

    public ScheduledMaintenanceService(
        IBackupService backup,
        IReportExportService reports,
        IEmailSender email,
        GoogleDriveBackupUploader drive)
    {
        _backup = backup;
        _reports = reports;
        _email = email;
        _drive = drive;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            // TreasurerDataPaths.TryImportFromLegacyPosBarData();
            CheckAndRunMissedBackup();
            ScheduleNextBackup();
            ScheduleDailyCheck();
        }
    }

    public void Dispose()
    {
        _backupTimer?.Dispose();
        _dailyCheckTimer?.Dispose();
    }

    private void ScheduleNextBackup()
    {
        var due = GetNextSaturday6Am() - DateTime.Now;
        if (due < TimeSpan.FromSeconds(1))
        {
            due = TimeSpan.FromSeconds(1);
        }

        _backupTimer?.Dispose();
        _backupTimer = new Timer(_ => _ = OnBackupTimerAsync(), null, due, Timeout.InfiniteTimeSpan);
    }

    private void ScheduleDailyCheck()
    {
        var now = DateTime.Now;
        var next7Am = now.Date.AddHours(7);
        if (next7Am <= now)
        {
            next7Am = next7Am.AddDays(1);
        }

        var due = next7Am - now;
        _dailyCheckTimer?.Dispose();
        _dailyCheckTimer = new Timer(
            _ =>
            {
                if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday && DateTime.Now.Hour >= 7)
                {
                    CheckAndRunMissedBackup();
                }

                ScheduleDailyCheck();
            },
            null,
            due,
            Timeout.InfiniteTimeSpan);
    }

    private void CheckAndRunMissedBackup()
    {
        var now = DateTime.Now;
        if (now.DayOfWeek != DayOfWeek.Saturday || now.Hour < 6)
        {
            return;
        }

        var backupsToday = Directory.Exists(TreasurerDataPaths.BackupDirectory)
            ? Directory.GetFiles(TreasurerDataPaths.BackupDirectory, $"NickeltownPOSV4_Backup_{now:yyyy-MM-dd}_*.zip")
            : Array.Empty<string>();

        if (backupsToday.Length == 0)
        {
            _ = RunScheduledBackupAsync();
        }
    }

    private async Task OnBackupTimerAsync()
    {
        try
        {
            await RunScheduledBackupAsync().ConfigureAwait(false);
        }
        finally
        {
            ScheduleNextBackup();
        }
    }

    public async Task RunScheduledBackupAsync(CancellationToken cancellationToken = default)
    {
        string? zipPath = null;
        try
        {
            zipPath = await _backup.CreateBackupAsync(TreasurerDataPaths.BackupDirectory, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
        }

        if (!string.IsNullOrEmpty(zipPath) && _drive.IsConfigured)
        {
            try
            {
                await _drive.UploadBackupFileAsync(zipPath, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        if (!TryClaimAutomatedStockEmailForToday())
        {
            return;
        }

        try
        {
            var bytes = await _reports.BuildStockSnapshotPdfAsync(cancellationToken).ConfigureAwait(false);
            var fileName = $"Stock_Snapshot_{DateTime.Now:yyyyMMdd}.pdf";
            await _email.SendAsync(
                subject: $"Nickeltown POS stock snapshot - {DateTime.Now:yyyy-MM-dd}",
                body:
                    "Attached is the weekly stock snapshot."
                    + Environment.NewLine + Environment.NewLine
                    + "Sent automatically by Nickeltown POS v4 (Saturday schedule).",
                attachments: [new EmailAttachment(fileName, bytes, "application/pdf")],
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseAutomatedStockEmailClaimIfSendFailed();
        }
    }

    private static string? GetSaturdayStockEmailFlagPathOrNull()
    {
        if (DateTime.Now.DayOfWeek != DayOfWeek.Saturday)
        {
            return null;
        }

        var datePart = DateTime.Now.ToString("yyyy-MM-dd");
        return TreasurerDataPaths.GetConfigFilePath($"saturday_weekly_stock_email_{datePart}.txt");
    }

    private static bool TryClaimAutomatedStockEmailForToday()
    {
        var flagPath = GetSaturdayStockEmailFlagPathOrNull();
        if (flagPath is null)
        {
            return true;
        }

        var datePart = DateTime.Now.ToString("yyyy-MM-dd");
        var mutexName = @"Global\NickeltownPOSV4_SaturdayStockEmail_" + datePart.Replace("-", string.Empty, StringComparison.Ordinal);

        try
        {
            using var mutex = new Mutex(false, mutexName);
            mutex.WaitOne();
            try
            {
                if (File.Exists(flagPath))
                {
                    return false;
                }

                File.WriteAllText(flagPath, $"{Environment.MachineName}{Environment.NewLine}{DateTime.Now:O}{Environment.NewLine}");
                return true;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch
        {
            return false;
        }
    }

    private static void ReleaseAutomatedStockEmailClaimIfSendFailed()
    {
        try
        {
            var flagPath = GetSaturdayStockEmailFlagPathOrNull();
            if (string.IsNullOrEmpty(flagPath))
            {
                return;
            }

            if (File.Exists(flagPath))
            {
                File.Delete(flagPath);
            }
        }
        catch
        {
        }
    }

    private static DateTime GetNextSaturday6Am()
    {
        var now = DateTime.Now;
        var daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilSaturday == 0 && now.Hour >= 6)
        {
            daysUntilSaturday = 7;
        }

        var nextSaturday = now.AddDays(daysUntilSaturday);
        return new DateTime(nextSaturday.Year, nextSaturday.Month, nextSaturday.Day, 6, 0, 0);
    }
}