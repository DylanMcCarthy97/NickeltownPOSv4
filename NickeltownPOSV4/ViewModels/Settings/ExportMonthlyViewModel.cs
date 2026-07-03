using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Views;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class ExportMonthlyViewModel : SettingsSubViewModelBase
{
    private readonly IReportExportService _reports;
    private readonly IEmailSender _email;
    private readonly IReportPathProvider _paths;
    private readonly IExportedFileLauncher _launcher;

    private DateTimeOffset _selectedMonth = DateTimeOffset.Now;

    public ExportMonthlyViewModel(
        INavigationService navigation,
        IReportExportService reports,
        IEmailSender email,
        IReportPathProvider paths,
        IExportedFileLauncher launcher)
        : base(navigation)
    {
        _reports = reports;
        _email = email;
        _paths = paths;
        _launcher = launcher;

        ExportPdfToFolderCommand = new AsyncRelayCommand(ExportPdfAsync, () => !IsBusy);
        ExportToFolderCommand = new AsyncRelayCommand(ExportCsvAsync, () => !IsBusy);
        EmailMonthlyCommand = new AsyncRelayCommand(EmailMonthlyAsync, () => !IsBusy);
        ExportStockPdfToFolderCommand = new AsyncRelayCommand(ExportStockPdfAsync, () => !IsBusy);
        SendStockEmailCommand = new AsyncRelayCommand(SendStockEmailAsync, () => !IsBusy);
        GoBackCommand = new RelayCommand(GoBack);
    }

    public IAsyncRelayCommand ExportPdfToFolderCommand { get; }

    public DateTimeOffset SelectedMonth
    {
        get => _selectedMonth;
        set => SetProperty(ref _selectedMonth, value);
    }

    public IAsyncRelayCommand ExportToFolderCommand { get; }

    public IAsyncRelayCommand EmailMonthlyCommand { get; }

    public IAsyncRelayCommand ExportStockPdfToFolderCommand { get; }

    public IAsyncRelayCommand SendStockEmailCommand { get; }

    public IRelayCommand GoBackCommand { get; }

    private void GoBack()
    {
        if (!TryNavigateBack())
        {
            Navigate(typeof(ReportsHomePage));
        }
    }

    private async Task ExportPdfAsync()
    {
        try
        {
            NotifyBusy(true);
            SetStatus("Building monthly PDF...");
            var bytes = await _reports.BuildMonthlyTabsPdfAsync(SelectedMonth.DateTime).ConfigureAwait(true);
            var fileName = $"Monthly_Bar_Tabs_{SelectedMonth:yyyyMM}.pdf";
            var path = await SaveAsync(_paths.GetBarTallyReportsDirectory(), bytes, fileName).ConfigureAwait(true);
            FinishExport(path);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}");
        }
        finally
        {
            NotifyBusy(false);
        }
    }

    private async Task ExportCsvAsync()
    {
        try
        {
            NotifyBusy(true);
            SetStatus("Building monthly CSV...");
            var bytes = await _reports.BuildMonthlyTabsCsvAsync(SelectedMonth.DateTime).ConfigureAwait(true);
            var fileName = $"NickeltownPOSV4_Monthly_{SelectedMonth:yyyy-MM}.csv";
            var path = await SaveAsync(_paths.GetBarTallyReportsDirectory(), bytes, fileName).ConfigureAwait(true);
            FinishExport(path);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}");
        }
        finally
        {
            NotifyBusy(false);
        }
    }

    private async Task EmailMonthlyAsync()
    {
        try
        {
            NotifyBusy(true);
            SetStatus("Building monthly PDF...");
            var bytes = await _reports.BuildMonthlyTabsPdfAsync(SelectedMonth.DateTime).ConfigureAwait(true);
            var fileName = $"Monthly_Bar_Tabs_{SelectedMonth:yyyyMM}.pdf";

            SetStatus("Sending email...");
            await _email.SendAsync(
                subject: $"Monthly Bar Tabs — {SelectedMonth:MMMM yyyy}",
                body:
                    $"Attached is the monthly bar tabs report for {SelectedMonth:MMMM yyyy}.{Environment.NewLine}{Environment.NewLine}"
                    + "Sent automatically by Nickeltown POS v4.",
                attachments: new List<EmailAttachment>
                {
                    new(fileName, bytes, "application/pdf"),
                })
                .ConfigureAwait(true);
            SetStatus("Monthly email sent.");
        }
        catch (Exception ex)
        {
            SetStatus($"Email failed: {ex.Message}");
        }
        finally
        {
            NotifyBusy(false);
        }
    }

    private async Task ExportStockPdfAsync()
    {
        try
        {
            NotifyBusy(true);
            SetStatus("Building stock PDF...");
            var bytes = await _reports.BuildStockSnapshotPdfAsync().ConfigureAwait(true);
            var fileName = $"Stock_Snapshot_{DateTime.Now:yyyyMMdd}.pdf";
            var path = await SaveAsync(_paths.GetStockReportsDirectory(), bytes, fileName).ConfigureAwait(true);
            FinishExport(path);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}");
        }
        finally
        {
            NotifyBusy(false);
        }
    }

    private async Task SendStockEmailAsync()
    {
        try
        {
            NotifyBusy(true);
            SetStatus("Building stock PDF...");
            var bytes = await _reports.BuildStockSnapshotPdfAsync().ConfigureAwait(true);
            var fileName = $"Stock_Snapshot_{DateTime.Now:yyyyMMdd}.pdf";

            SetStatus("Sending stock email...");
            await _email.SendAsync(
                subject: $"Nickeltown POS stock snapshot — {DateTime.Now:yyyy-MM-dd}",
                body:
                    "Attached is the current stock snapshot."
                    + Environment.NewLine + Environment.NewLine
                    + "Sent automatically by Nickeltown POS v4.",
                attachments: new List<EmailAttachment>
                {
                    new(fileName, bytes, "application/pdf"),
                })
                .ConfigureAwait(true);
            SetStatus("Stock email sent.");
        }
        catch (Exception ex)
        {
            SetStatus($"Stock email failed: {ex.Message}");
        }
        finally
        {
            NotifyBusy(false);
        }
    }

    /// <summary>
    /// Writes <paramref name="bytes"/> into <paramref name="directory"/>/<paramref name="fileName"/>,
    /// creating the directory if needed, and returns the resolved full path.
    /// </summary>
    private static async Task<string> SaveAsync(string directory, byte[] bytes, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);
        return path;
    }

    private void FinishExport(string path)
    {
        if (_launcher.TryLaunch(path))
        {
            SetStatus($"Saved and opened: {path}");
            return;
        }

        if (_launcher.RevealInExplorer(path))
        {
            SetStatus($"Saved to: {path} (no default app — opened in Explorer).");
            return;
        }

        SetStatus($"Saved to: {path}");
    }

    private void NotifyBusy(bool busy)
    {
        IsBusy = busy;
        ExportPdfToFolderCommand.NotifyCanExecuteChanged();
        ExportToFolderCommand.NotifyCanExecuteChanged();
        EmailMonthlyCommand.NotifyCanExecuteChanged();
        ExportStockPdfToFolderCommand.NotifyCanExecuteChanged();
        SendStockEmailCommand.NotifyCanExecuteChanged();
    }
}
