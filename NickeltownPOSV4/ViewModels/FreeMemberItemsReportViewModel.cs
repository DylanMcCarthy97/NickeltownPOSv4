using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Complimentary;
using NickeltownPOSV4.Views;

namespace NickeltownPOSV4.ViewModels;

public sealed class ComplimentaryReportLineViewModel
{
    public ComplimentaryReportLineViewModel(ComplimentaryReportLine line)
    {
        ItemName = line.ItemName;
        Quantity = line.Quantity;
        UnitRetailPrice = line.UnitRetailPrice;
        RetailValue = line.RetailValue;
        QuantityText = line.Quantity.ToString(CultureInfo.CurrentCulture);
        PriceText = line.UnitRetailPrice > 0m
            ? $"{line.Quantity.ToString(CultureInfo.CurrentCulture)} × {line.UnitRetailPrice.ToString("C2", CultureInfo.CurrentCulture)}"
            : line.Quantity.ToString(CultureInfo.CurrentCulture);
        RetailValueText = line.RetailValue.ToString("C2", CultureInfo.CurrentCulture);
    }

    public string ItemName { get; }

    public int Quantity { get; }

    public decimal UnitRetailPrice { get; }

    public decimal RetailValue { get; }

    public string QuantityText { get; }

    public string PriceText { get; }

    public string RetailValueText { get; }
}

public sealed class FreeMemberItemsReportViewModel : ObservableViewModel
{
    private readonly IComplimentaryItemService _complimentary;
    private readonly INavigationService _navigation;
    private readonly IInputOverlayService _inputOverlay;

    private DateTimeOffset _fromDate = new(DateTime.Today);
    private DateTimeOffset _toDate = new(DateTime.Today);
    private string _statusMessage = string.Empty;
    private string _totalItemsText = "0";
    private string _totalRetailText = "$0.00";
    private bool _isBusy;

    public FreeMemberItemsReportViewModel(
        IComplimentaryItemService complimentary,
        INavigationService navigation,
        IInputOverlayService inputOverlay)
    {
        _complimentary = complimentary;
        _navigation = navigation;
        _inputOverlay = inputOverlay;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        BackCommand = new RelayCommand(() => _navigation.Navigate(typeof(ReportsHomePage)));
        TodayCommand = new AsyncRelayCommand(LoadTodayAsync);
        EditFromDateCommand = new AsyncRelayCommand(EditFromDateAsync);
        EditToDateCommand = new AsyncRelayCommand(EditToDateAsync);
    }

    public ObservableCollection<ComplimentaryReportLineViewModel> Lines { get; } = new();

    public DateTimeOffset FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                OnPropertyChanged(nameof(FromDateText));
            }
        }
    }

    public DateTimeOffset ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                OnPropertyChanged(nameof(ToDateText));
            }
        }
    }

    public string FromDateText => FromDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public string ToDateText => ToDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value ?? string.Empty);
    }

    public string TotalItemsText
    {
        get => _totalItemsText;
        private set => SetProperty(ref _totalItemsText, value);
    }

    public string TotalRetailText
    {
        get => _totalRetailText;
        private set => SetProperty(ref _totalRetailText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand BackCommand { get; }

    public IAsyncRelayCommand TodayCommand { get; }

    public IAsyncRelayCommand EditFromDateCommand { get; }

    public IAsyncRelayCommand EditToDateCommand { get; }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var from = DateOnly.FromDateTime(FromDate.LocalDateTime.Date);
            var to = DateOnly.FromDateTime(ToDate.LocalDateTime.Date);
            var report = await _complimentary.GetReportAsync(from, to).ConfigureAwait(true);
            Lines.Clear();
            foreach (var line in report.Lines)
            {
                Lines.Add(new ComplimentaryReportLineViewModel(line));
            }

            TotalItemsText = report.TotalItems.ToString(CultureInfo.CurrentCulture);
            TotalRetailText = report.TotalRetailValue.ToString("C2", CultureInfo.CurrentCulture);
            StatusMessage = report.TotalItems == 0
                ? "No free member items in this range. Retail value is informational only and is not recorded as revenue."
                : "Retail value is informational only and is not recorded as revenue.";
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

    private async Task LoadTodayAsync()
    {
        var today = DateTime.Today;
        FromDate = new DateTimeOffset(today);
        ToDate = new DateTimeOffset(today);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task EditFromDateAsync() => await EditDateAsync(isFrom: true).ConfigureAwait(true);

    private async Task EditToDateAsync() => await EditDateAsync(isFrom: false).ConfigureAwait(true);

    private async Task EditDateAsync(bool isFrom)
    {
        var current = DateOnly.FromDateTime((isFrom ? FromDate : ToDate).LocalDateTime.Date);
        var result = await _inputOverlay
            .ShowDatePickerAsync(current, isFrom ? "From date" : "Through date")
            .ConfigureAwait(true);
        if (!result.HasSelection)
        {
            return;
        }

        var picked = result.Value!.Value.ToDateTime(TimeOnly.MinValue);
        if (isFrom)
        {
            FromDate = new DateTimeOffset(picked);
        }
        else
        {
            ToDate = new DateTimeOffset(picked);
        }

        await RefreshAsync().ConfigureAwait(true);
    }
}
