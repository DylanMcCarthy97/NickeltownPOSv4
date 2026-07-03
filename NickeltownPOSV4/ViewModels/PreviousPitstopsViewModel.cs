using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Views;

namespace NickeltownPOSV4.ViewModels;

public sealed class PreviousPitstopsViewModel : ObservableViewModel
{
    private readonly INavigationService _navigation;
    private readonly IPitstopEodBatchRepository _batches;
    private readonly IUserSessionService _session;
    private readonly IExportedFileLauncher _launcher;
    private readonly IReportPathProvider _paths;

    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public PreviousPitstopsViewModel(
        INavigationService navigation,
        IPitstopEodBatchRepository batches,
        IUserSessionService session,
        IExportedFileLauncher launcher,
        IReportPathProvider paths)
    {
        _navigation = navigation;
        _batches = batches;
        _session = session;
        _launcher = launcher;
        _paths = paths;
        Rows = new PagedCollection<PreviousPitstopRowViewModel>(pageSize: 4);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        BackCommand = new RelayCommand(() => _navigation.TryGoBack());
    }

    public PagedCollection<PreviousPitstopRowViewModel> Rows { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

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
        if (!_session.CanAccessReports)
        {
            StatusMessage = "Admin access required.";
            _navigation.TryGoBack();
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    public async Task RefreshAsync()
    {
        if (!_session.CanAccessReports)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading previous Pitstops...";
            var data = await _batches.ListBatchesAsync().ConfigureAwait(true);
            Rows.Replace(data.Select(r => new PreviousPitstopRowViewModel(r, NavigateToDetail, NavigateToItems, NavigateToTransactions, ReprintReport)));
            StatusMessage = Rows.TotalCount == 0
                ? "No archived Pitstop events yet."
                : $"{Rows.TotalCount} archived event(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NavigateToDetail(long batchId) =>
        _navigation.Navigate(typeof(PreviousPitstopDetailPage), batchId);

    private void NavigateToItems(long batchId) =>
        _navigation.Navigate(typeof(PreviousPitstopItemsPage), batchId);

    private void NavigateToTransactions(long batchId) =>
        _navigation.Navigate(typeof(PreviousPitstopTransactionsPage), batchId);

    private async void ReprintReport(long batchId)
    {
        var detail = await _batches.GetBatchDetailAsync(batchId).ConfigureAwait(true);
        if (detail?.ReportData is null)
        {
            StatusMessage = "No saved report snapshot for this event.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(detail.PdfPath) && System.IO.File.Exists(detail.PdfPath))
        {
            StatusMessage = _launcher.TryLaunch(detail.PdfPath)
                ? $"Opened saved report: {detail.PdfPath}"
                : $"Report file: {detail.PdfPath}";
            return;
        }

        try
        {
            var bytes = PitstopReportPdfExporter.Build(detail.ReportData);
            var dir = _paths.GetPitstopReportsDirectory();
            System.IO.Directory.CreateDirectory(dir);
            var safe = string.Join(
                "_",
                (detail.EventName ?? "pitstop").Split(System.IO.Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrEmpty(safe))
            {
                safe = "pitstop";
            }

            var fn = $"{safe}_{detail.ArchivedAt.LocalDateTime:yyyyMMdd}_{detail.ArchivedAt.LocalDateTime:HHmmss}_reprint.pdf";
            var path = System.IO.Path.Combine(dir, fn);
            await System.IO.File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);
            StatusMessage = _launcher.TryLaunch(path) ? $"Reprinted: {path}" : $"Saved: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reprint failed: {ex.Message}";
        }
    }
}

public sealed class PreviousPitstopRowViewModel : ObservableViewModel
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    private readonly Action<long> _viewDetails;
    private readonly Action<long> _viewItems;
    private readonly Action<long> _viewTransactions;
    private readonly Action<long> _reprint;

    public PreviousPitstopRowViewModel(
        PitstopEodBatchListRow row,
        Action<long> viewDetails,
        Action<long> viewItems,
        Action<long> viewTransactions,
        Action<long> reprint)
    {
        BatchId = row.Id;
        _viewDetails = viewDetails;
        _viewItems = viewItems;
        _viewTransactions = viewTransactions;
        _reprint = reprint;

        ArchivedAtText = row.ArchivedAt.ToString("yyyy-MM-dd HH:mm", Cult);
        EventName = string.IsNullOrWhiteSpace(row.EventName) ? "Pitstop" : row.EventName;
        TotalSalesText = row.TotalSales.ToString("C2", Cult);
        CashText = row.CashTotal.ToString("C2", Cult);
        CardText = row.CardChargedTotal.ToString("C2", Cult);
        SurchargeText = row.CardSurchargeTotal.ToString("C2", Cult);
        FeesText = row.EstimatedSquareFees.ToString("C2", Cult);
        NetText = row.NetTotal.ToString("C2", Cult);
        SaleCountText = row.SaleCount.ToString(Cult);
        OperatorText = string.IsNullOrWhiteSpace(row.OperatorName) ? "—" : row.OperatorName;
        TotalsSummaryText =
            $"Total {TotalSalesText} · Cash {CashText} · Card {CardText} · Fees {FeesText} · Net {NetText}";

        ViewDetailsCommand = new RelayCommand(() => _viewDetails(BatchId));
        ReprintCommand = new RelayCommand(() => _reprint(BatchId));
        ViewItemsCommand = new RelayCommand(() => _viewItems(BatchId));
        ViewTransactionsCommand = new RelayCommand(() => _viewTransactions(BatchId));
    }

    public long BatchId { get; }

    public string ArchivedAtText { get; }

    public string EventName { get; }

    public string TotalSalesText { get; }

    public string CashText { get; }

    public string CardText { get; }

    public string SurchargeText { get; }

    public string FeesText { get; }

    public string NetText { get; }

    public string SaleCountText { get; }

    public string OperatorText { get; }

    public string TotalsSummaryText { get; }

    public IRelayCommand ViewDetailsCommand { get; }

    public IRelayCommand ReprintCommand { get; }

    public IRelayCommand ViewItemsCommand { get; }

    public IRelayCommand ViewTransactionsCommand { get; }
}
