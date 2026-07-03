using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class PreviousPitstopItemsViewModel : ObservableViewModel
{
    private readonly IPitstopEodBatchRepository _batches;
    private readonly IUserSessionService _session;
    private readonly INavigationService _navigation;

    private string _title = "Items";
    private string _statusMessage = string.Empty;

    public PreviousPitstopItemsViewModel(
        IPitstopEodBatchRepository batches,
        IUserSessionService session,
        INavigationService navigation)
    {
        _batches = batches;
        _session = session;
        _navigation = navigation;
        Rows = new PagedCollection<PreviousPitstopItemRowVm>(pageSize: 8);
        BackCommand = new RelayCommand(() => _navigation.TryGoBack());
    }

    public PagedCollection<PreviousPitstopItemRowVm> Rows { get; }

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
            Title = $"{detail?.EventName ?? "Pitstop"} — items";

            if (detail?.ReportData?.PitstopProductSales is { Count: > 0 } products)
            {
                Rows.Replace(
                    products
                        .OrderByDescending(p => p.LineTotal)
                        .Select(p => new PreviousPitstopItemRowVm(p.Name, p.CategoryName, p.Quantity, p.LineTotal)));
            }
            else
            {
                var lines = await _batches.GetBatchItemisedLinesAsync(batchId).ConfigureAwait(true);
                Rows.Replace(
                    lines
                        .Where(l => l.ItemId > 0)
                        .GroupBy(l => (l.ItemId, l.ItemName, l.CategoryName))
                        .OrderByDescending(g => g.Sum(x => x.LineTotal))
                        .Select(g => new PreviousPitstopItemRowVm(
                            g.Key.ItemName,
                            g.Key.CategoryName ?? string.Empty,
                            g.Sum(x => x.Quantity),
                            decimal.Round(g.Sum(x => x.LineTotal), 2, MidpointRounding.AwayFromZero))));
            }

            StatusMessage = Rows.TotalCount == 0 ? "No item lines for this event." : $"{Rows.TotalCount} product line(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }
}
