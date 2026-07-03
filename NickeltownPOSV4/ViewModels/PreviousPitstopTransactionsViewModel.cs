using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class PreviousPitstopTransactionsViewModel : ObservableViewModel
{
    private readonly IPitstopEodBatchRepository _batches;
    private readonly IUserSessionService _session;
    private readonly INavigationService _navigation;

    private string _title = "Transactions";
    private string _statusMessage = string.Empty;

    public PreviousPitstopTransactionsViewModel(
        IPitstopEodBatchRepository batches,
        IUserSessionService session,
        INavigationService navigation)
    {
        _batches = batches;
        _session = session;
        _navigation = navigation;
        Rows = new PagedCollection<PreviousPitstopTransactionRowVm>(pageSize: 7);
        BackCommand = new RelayCommand(() => _navigation.TryGoBack());
    }

    public PagedCollection<PreviousPitstopTransactionRowVm> Rows { get; }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public IRelayCommand BackCommand { get; }

    public async Task LoadAsync(long batchId)
    {
        if (!_session.CanAccessReports)
        {
            _navigation.TryGoBack();
            return;
        }

        try
        {
            var detail = await _batches.GetBatchDetailAsync(batchId).ConfigureAwait(true);
            Title = $"{detail?.EventName ?? "Pitstop"} — transactions";
            var sales = await _batches.GetBatchSalesAsync(batchId).ConfigureAwait(true);
            Rows.Replace(sales.Select(s => new PreviousPitstopTransactionRowVm(s)));
            StatusMessage = Rows.TotalCount == 0 ? "No transactions." : $"{Rows.TotalCount} transaction(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}

public sealed class PreviousPitstopTransactionRowVm
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    public PreviousPitstopTransactionRowVm(Models.Pitstop.PitstopArchivedSaleRow row)
    {
        SoldAtText = row.SoldAt.ToString("yyyy-MM-dd HH:mm", Cult);
        PaymentMethod = row.PaymentMethod;
        TotalText = row.Total.ToString("C2", Cult);
        BaseText = row.BaseProductTotal?.ToString("C2", Cult) ?? "—";
        SurchargeText = row.CardSurchargeAmount?.ToString("C2", Cult) ?? "—";
        StaffText = row.StaffDisplayName ?? "—";
        SquareRefText = string.IsNullOrWhiteSpace(row.SquareExternalRef) ? "—" : row.SquareExternalRef;
    }

    public string SoldAtText { get; }

    public string PaymentMethod { get; }

    public string TotalText { get; }

    public string BaseText { get; }

    public string SurchargeText { get; }

    public string StaffText { get; }

    public string SquareRefText { get; }
}
