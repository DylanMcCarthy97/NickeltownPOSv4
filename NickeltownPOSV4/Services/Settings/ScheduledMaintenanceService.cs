using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.Services.Treasurer;

namespace NickeltownPOSV4.Services.Settings;

/// <summary>Saturday 6 AM backup, Google Drive upload, weekly stock email, and shopping-list email.</summary>
public sealed class ScheduledMaintenanceService : IDisposable
{
    private readonly IBackupService _backup;
    private readonly IReportExportService _reports;
    private readonly IEmailSender _email;
    private readonly IShoppingListEmailService _shoppingListEmail;
    private readonly GoogleDriveBackupUploader _drive;
    private readonly ILogger<ScheduledMaintenanceService> _logger;
    private readonly object _gate = new();
    private Timer? _backupTimer;
    private Timer? _dailyCheckTimer;
    private bool _started;

    public ScheduledMaintenanceService(
        IBackupService backup,
        IReportExportService reports,
        IEmailSender email,
        IShoppingListEmailService shoppingListEmail,
        GoogleDriveBackupUploader drive,
        ILogger<ScheduledMaintenanceService> logger)
    {
        _backup = backup;
        _reports = reports;
        _email = email;
        _shoppingListEmail = shoppingListEmail;
        _drive = drive;
        _logger = logger;
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
        else
        {
            // Backup already done — still retry unclaimed Saturday emails (stock / shopping list).
            _ = RunScheduledEmailsAsync();
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Saturday backup zip failed.");
        }

        if (!string.IsNullOrEmpty(zipPath) && _drive.IsConfigured)
        {
            try
            {
                await _drive.UploadBackupFileAsync(zipPath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Saturday Google Drive upload failed.");
            }
        }

        await RunScheduledEmailsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stock snapshot + shopping-list emails with independent once-per-Saturday claims.</summary>
    public async Task RunScheduledEmailsAsync(CancellationToken cancellationToken = default)
    {
        // Stock snapshot email — unchanged recipients (AppEmailConfig.RecipientEmails).
        if (SaturdayEmailClaim.TryClaim(SaturdayEmailClaim.StockKind))
        {
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
                _logger.LogInformation("Saturday stock snapshot email sent.");
            }
            catch (Exception ex)
            {
                SaturdayEmailClaim.ReleaseIfSendFailed(SaturdayEmailClaim.StockKind);
                _logger.LogError(ex, "Saturday stock snapshot email failed.");
            }
        }

        // Shopping-list email is independent: failure here never affects the stock report claim.
        try
        {
            var shopping = await _shoppingListEmail.TrySendScheduledAsync(cancellationToken).ConfigureAwait(false);
            if (!shopping.Succeeded && !shopping.Skipped)
            {
                _logger.LogWarning("Saturday shopping-list email: {Message}", shopping.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saturday shopping-list email threw unexpectedly.");
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
