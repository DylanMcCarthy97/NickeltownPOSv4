using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;

namespace NickeltownPOSV4.ViewModels;

public sealed class PreviousPitstopDetailViewModel : ObservableViewModel
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    private readonly IPitstopEodBatchRepository _batches;
    private readonly IUserSessionService _session;
    private readonly IExportedFileLauncher _launcher;
    private readonly IReportPathProvider _paths;
    private readonly INavigationService _navigation;
    private readonly IInputOverlayService _input;
    private readonly IAuditLogService _audit;

    private long _batchId;
    private string _title = "Pitstop event";
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private string _notesText = "(no notes)";
    private string _cashCountText = string.Empty;
    private string _backupBeforeText = "—";
    private string _backupAfterText = "—";

    public PreviousPitstopDetailViewModel(
        IPitstopEodBatchRepository batches,
        IUserSessionService session,
        IExportedFileLauncher launcher,
        IReportPathProvider paths,
        INavigationService navigation,
        IInputOverlayService input,
        IAuditLogService audit)
    {
        _batches = batches;
        _session = session;
        _launcher = launcher;
        _paths = paths;
        _navigation = navigation;
        _input = input;
        _audit = audit;
        Sales = new ObservableCollection<PreviousPitstopSaleLineVm>();
        PaymentBreakdown = new ObservableCollection<PreviousPitstopPaymentRowVm>();
        ItemBreakdown = new ObservableCollection<PreviousPitstopItemRowVm>();
        Warnings = new ObservableCollection<string>();
        BackCommand = new RelayCommand(() => _navigation.TryGoBack());
        ReprintCommand = new AsyncRelayCommand(ReprintAsync);
        AddNoteCommand = new AsyncRelayCommand(AddNoteAsync, () => _session.IsManager && !IsBusy);
    }

    public ObservableCollection<PreviousPitstopSaleLineVm> Sales { get; }

    public ObservableCollection<PreviousPitstopPaymentRowVm> PaymentBreakdown { get; }

    public ObservableCollection<PreviousPitstopItemRowVm> ItemBreakdown { get; }

    public ObservableCollection<string> Warnings { get; }

    public bool HasWarnings => Warnings.Count > 0;

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string SummaryText { get; private set; } = string.Empty;

    public string CashCardSplitText { get; private set; } = string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                AddNoteCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string NotesText
    {
        get => _notesText;
        private set => SetProperty(ref _notesText, value);
    }

    public string CashCountText
    {
        get => _cashCountText;
        private set => SetProperty(ref _cashCountText, value);
    }

    public string BackupBeforeText
    {
        get => _backupBeforeText;
        private set => SetProperty(ref _backupBeforeText, value);
    }

    public string BackupAfterText
    {
        get => _backupAfterText;
        private set => SetProperty(ref _backupAfterText, value);
    }

    public bool CanAddNote => _session.IsManager;

    public IRelayCommand BackCommand { get; }

    public IAsyncRelayCommand ReprintCommand { get; }

    public IAsyncRelayCommand AddNoteCommand { get; }

    public async Task LoadAsync(long batchId)
    {
        if (!_session.CanAccessReports)
        {
            _navigation.TryGoBack();
            return;
        }

        _batchId = batchId;
        IsBusy = true;
        try
        {
            var detail = await _batches.GetBatchDetailAsync(batchId).ConfigureAwait(true);
            if (detail is null)
            {
                StatusMessage = "Archived Pitstop event not found.";
                return;
            }

            Title = $"{detail.EventName ?? "Pitstop"} — {detail.ArchivedAt.LocalDateTime:yyyy-MM-dd HH:mm}";
            SummaryText =
                $"Total {detail.TotalSales:C2} · Net {detail.NetTotal:C2} · {detail.SaleCount} sale(s) · archived by {detail.OperatorName ?? "—"}";
            CashCardSplitText =
                $"Cash {detail.CashTotal:C2} · Card {detail.CardChargedTotal:C2} · Surcharge {detail.CardSurchargeTotal:C2} · Est. Square fees {detail.EstimatedSquareFees:C2}";
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(CashCardSplitText));

            NotesText = string.IsNullOrWhiteSpace(detail.Notes) ? "(no notes)" : detail.Notes;
            CashCountText = BuildCashCountText(detail);
            BackupBeforeText = string.IsNullOrWhiteSpace(detail.BackupBeforePath) ? "—" : detail.BackupBeforePath;
            BackupAfterText = string.IsNullOrWhiteSpace(detail.BackupAfterPath) ? "—" : detail.BackupAfterPath;

            Warnings.Clear();
            foreach (var w in detail.ReconciliationWarnings)
            {
                Warnings.Add(w);
            }

            OnPropertyChanged(nameof(HasWarnings));
            OnPropertyChanged(nameof(CanAddNote));

            var sales = await _batches.GetBatchSalesAsync(batchId).ConfigureAwait(true);
            Sales.Clear();
            foreach (var s in sales)
            {
                Sales.Add(new PreviousPitstopSaleLineVm(s));
            }

            PaymentBreakdown.Clear();
            if (detail.ReportData?.PitstopPaymentBreakdown is { Count: > 0 } payments)
            {
                foreach (var p in payments)
                {
                    PaymentBreakdown.Add(new PreviousPitstopPaymentRowVm(p.PaymentMethod, p.Total));
                }
            }
            else
            {
                foreach (var g in sales.GroupBy(s => s.PaymentMethod, StringComparer.OrdinalIgnoreCase))
                {
                    PaymentBreakdown.Add(
                        new PreviousPitstopPaymentRowVm(
                            g.Key,
                            decimal.Round(g.Sum(x => x.Total), 2, MidpointRounding.AwayFromZero)));
                }
            }

            ItemBreakdown.Clear();
            if (detail.ReportData?.PitstopProductSales is { Count: > 0 } products)
            {
                foreach (var p in products.OrderByDescending(x => x.LineTotal))
                {
                    ItemBreakdown.Add(new PreviousPitstopItemRowVm(p.Name, p.CategoryName, p.Quantity, p.LineTotal));
                }
            }
            else
            {
                var lines = await _batches.GetBatchItemisedLinesAsync(batchId).ConfigureAwait(true);
                foreach (var g in lines
                             .Where(l => l.ItemId > 0)
                             .GroupBy(l => (l.ItemId, l.ItemName, l.CategoryName))
                             .OrderByDescending(g => g.Sum(x => x.LineTotal)))
                {
                    ItemBreakdown.Add(
                        new PreviousPitstopItemRowVm(
                            g.Key.ItemName,
                            g.Key.CategoryName ?? string.Empty,
                            g.Sum(x => x.Quantity),
                            decimal.Round(g.Sum(x => x.LineTotal), 2, MidpointRounding.AwayFromZero)));
                }
            }

            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReprintAsync()
    {
        var detail = await _batches.GetBatchDetailAsync(_batchId).ConfigureAwait(true);
        if (detail?.ReportData is null)
        {
            StatusMessage = "No saved report snapshot for reprint.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(detail.PdfPath) && System.IO.File.Exists(detail.PdfPath))
        {
            StatusMessage = _launcher.TryLaunch(detail.PdfPath) ? "Opened saved PDF." : detail.PdfPath;
            return;
        }

        try
        {
            var bytes = PitstopReportPdfExporter.Build(detail.ReportData);
            var dir = _paths.GetPitstopReportsDirectory();
            System.IO.Directory.CreateDirectory(dir);
            var fn = $"pitstop_{detail.ArchivedAt.LocalDateTime:yyyyMMdd}_{detail.ArchivedAt.LocalDateTime:HHmmss}_reprint.pdf";
            var path = System.IO.Path.Combine(dir, fn);
            await System.IO.File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);
            StatusMessage = _launcher.TryLaunch(path) ? $"Reprinted: {path}" : $"Saved: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reprint failed: {ex.Message}";
        }
    }

    private async Task AddNoteAsync()
    {
        if (!_session.IsManager)
        {
            StatusMessage = "Admin/Treasurer access required to add a note.";
            return;
        }

        var note = await _input.ShowKeyboardAsync(string.Empty, "Add note to this Pitstop event", CancellationToken.None).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var ok = await _batches.AppendNoteAsync(_batchId, note).ConfigureAwait(true);
            if (!ok)
            {
                StatusMessage = "Could not save note (batch not found).";
                return;
            }

            try
            {
                await _audit.LogAsync(
                    AuditActions.PitstopArchiveNoteAdded,
                    AuditEntityTypes.PitstopEodBatch,
                    entityId: _batchId.ToString(Cult),
                    reason: note.Trim()).ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }

            await LoadAsync(_batchId).ConfigureAwait(true);
            StatusMessage = "Note saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Add note failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string BuildCashCountText(PitstopEodBatchDetail detail)
    {
        var hasFloat = detail.StartingFloat > 0m;
        var hasCounted = detail.CashCounted is not null;
        var hasRemoved = detail.FloatRemoved is not null;
        var hasExpected = detail.ExpectedCash is not null;
        var hasVariance = detail.CashVariance is not null;

        if (!hasFloat && !hasCounted && !hasRemoved && !hasExpected && !hasVariance)
        {
            return "No cash count recorded.";
        }

        var parts = new System.Collections.Generic.List<string>();
        parts.Add($"Starting float {detail.StartingFloat:C2}");
        if (hasExpected)
        {
            parts.Add($"Expected {detail.ExpectedCash:C2}");
        }

        if (hasCounted)
        {
            parts.Add($"Counted {detail.CashCounted:C2}");
        }

        if (hasRemoved)
        {
            parts.Add($"Float removed {detail.FloatRemoved:C2}");
        }

        if (hasVariance)
        {
            parts.Add($"Variance {detail.CashVariance:C2}");
        }

        return string.Join(" · ", parts);
    }
}

public sealed class PreviousPitstopSaleLineVm
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    public PreviousPitstopSaleLineVm(PitstopArchivedSaleRow row)
    {
        SoldAtText = row.SoldAt.ToString("HH:mm", Cult);
        PaymentMethod = row.PaymentMethod;
        TotalText = row.Total.ToString("C2", Cult);
        StaffText = string.IsNullOrWhiteSpace(row.StaffDisplayName) ? string.Empty : $" · {row.StaffDisplayName}";
    }

    public string SoldAtText { get; }

    public string PaymentMethod { get; }

    public string TotalText { get; }

    public string StaffText { get; }
}

public sealed class PreviousPitstopPaymentRowVm
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    public PreviousPitstopPaymentRowVm(string method, decimal total)
    {
        PaymentMethod = method;
        TotalText = total.ToString("C2", Cult);
    }

    public string PaymentMethod { get; }

    public string TotalText { get; }
}

public sealed class PreviousPitstopItemRowVm
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    public PreviousPitstopItemRowVm(string name, string category, int qty, decimal total)
    {
        Name = name;
        Category = category;
        QtyText = qty.ToString(Cult);
        TotalText = total.ToString("C2", Cult);
    }

    public string Name { get; }

    public string Category { get; }

    public string QtyText { get; }

    public string TotalText { get; }
}
