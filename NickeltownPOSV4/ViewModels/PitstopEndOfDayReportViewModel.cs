using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Views;

namespace NickeltownPOSV4.ViewModels;

public sealed class OutsideLineEditVm : ObservableViewModel
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly IInputOverlayService _input;
    private readonly Action _onValuesChanged;

    private int _soldQty;

    public OutsideLineEditVm(IInputOverlayService input, OutsideItemSaleRow seed, Action onValuesChanged)
    {
        _input = input;
        _onValuesChanged = onValuesChanged;
        Key = seed.Key;
        DisplayLabel = seed.DisplayLabel;
        OutsideLineKind = seed.OutsideLineKind ?? string.Empty;
        PitstopItemId = seed.PitstopItemId;
        SuggestedUnitPrice = seed.SuggestedUnitPrice;
        _soldQty = seed.SoldQty > 0 ? seed.SoldQty : Math.Max(0, seed.CashQty + seed.CardQty);

        IncrementQtyCommand = new RelayCommand(() => SoldQty++);
        DecrementQtyCommand = new RelayCommand(() =>
        {
            if (SoldQty > 0)
            {
                SoldQty--;
            }
        });
        BeginQtyCommand = new AsyncRelayCommand(BeginQtyAsync);
    }

    public string Key { get; }

    public string DisplayLabel { get; }

    public string OutsideLineKind { get; }

    public long? PitstopItemId { get; }

    public decimal? SuggestedUnitPrice { get; }

    public bool IsRaffle =>
        string.Equals(OutsideLineKind, PitstopOutsideLineCatalogBuilder.LineKindRaffle, StringComparison.Ordinal);

    public bool IsMerch =>
        string.Equals(OutsideLineKind, PitstopOutsideLineCatalogBuilder.LineKindMerchSku, StringComparison.Ordinal);

    public bool HasAnyValue => SoldQty > 0;

    public string SuggestedPriceText =>
        SuggestedUnitPrice is decimal p && p > 0m ? $"${p:0.00}" : string.Empty;

    public string RowTotalText => Money(LineSales);

    public decimal LineSales => PitstopEodCalculator.LineSales(SoldQty, SuggestedUnitPrice);

    public int SoldQty
    {
        get => _soldQty;
        set
        {
            var next = value < 0 ? 0 : value;
            if (SetProperty(ref _soldQty, next))
            {
                OnPropertyChanged(nameof(SoldQtyText));
                OnPropertyChanged(nameof(HasAnyValue));
                OnPropertyChanged(nameof(RowTotalText));
                OnPropertyChanged(nameof(LineSales));
                _onValuesChanged();
            }
        }
    }

    public string SoldQtyText => _soldQty.ToString(Inv);

    public IRelayCommand IncrementQtyCommand { get; }

    public IRelayCommand DecrementQtyCommand { get; }

    public IAsyncRelayCommand BeginQtyCommand { get; }

    public OutsideItemSaleRow ToModel() =>
        new()
        {
            Key = Key,
            DisplayLabel = DisplayLabel,
            OutsideLineKind = OutsideLineKind,
            PitstopItemId = PitstopItemId,
            SuggestedUnitPrice = SuggestedUnitPrice,
            SoldQty = SoldQty,
            CashQty = SoldQty,
            CashDollars = LineSales,
            CardQty = 0,
            CardDollars = 0m,
        };

    private async Task BeginQtyAsync()
    {
        var r = await _input.ShowIntegerNumpadAsync(SoldQty, $"{DisplayLabel} — qty sold", 0, 9999999, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            SoldQty = r.Value;
        }
    }

    private static string Money(decimal v) => v.ToString("0.00", Inv);
}

public sealed class EventExpenseEditVm : ObservableViewModel
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly IInputOverlayService _input;
    private readonly Action _onValuesChanged;

    private string _description = string.Empty;
    private decimal _amount;
    private EventExpensePaymentSource _paidFrom;
    private EventExpenseKind _kind;

    public EventExpenseEditVm(
        IInputOverlayService input,
        Action onValuesChanged,
        EventExpenseKind kind = EventExpenseKind.Expense)
    {
        _input = input;
        _onValuesChanged = onValuesChanged;
        _kind = kind;
        _paidFrom = kind == EventExpenseKind.CashPrize
            ? EventExpensePaymentSource.InsideTill
            : EventExpensePaymentSource.Other;
        if (kind == EventExpenseKind.CashPrize)
        {
            _description = "Money Wheel";
        }

        BeginDescriptionCommand = new AsyncRelayCommand(BeginDescriptionAsync);
        BeginAmountCommand = new AsyncRelayCommand(BeginAmountAsync);
        CyclePaidFromCommand = new RelayCommand(CyclePaidFrom);
        SetPaidFromInsideCommand = new RelayCommand(() => PaidFrom = EventExpensePaymentSource.InsideTill);
        SetPaidFromOutsideCommand = new RelayCommand(() => PaidFrom = EventExpensePaymentSource.OutsideTin);
        SetPaidFromBankCommand = new RelayCommand(() => PaidFrom = EventExpensePaymentSource.Other);
    }

    public bool IsCashPrize => Kind == EventExpenseKind.CashPrize;

    public EventExpenseKind Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                OnPropertyChanged(nameof(IsCashPrize));
                OnPropertyChanged(nameof(PaidFromText));
                _onValuesChanged();
            }
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value ?? string.Empty))
            {
                _onValuesChanged();
            }
        }
    }

    public decimal Amount
    {
        get => _amount;
        set
        {
            if (SetProperty(ref _amount, value))
            {
                OnPropertyChanged(nameof(AmountText));
                _onValuesChanged();
            }
        }
    }

    public string AmountText => _amount.ToString("0.00", Inv);

    public EventExpensePaymentSource PaidFrom
    {
        get => _paidFrom;
        set
        {
            if (SetProperty(ref _paidFrom, value))
            {
                OnPropertyChanged(nameof(PaidFromText));
                OnPropertyChanged(nameof(IsPaidFromInside));
                OnPropertyChanged(nameof(IsPaidFromOutside));
                OnPropertyChanged(nameof(IsPaidFromBank));
                _onValuesChanged();
            }
        }
    }

    public bool IsPaidFromInside => PaidFrom == EventExpensePaymentSource.InsideTill;

    public bool IsPaidFromOutside => PaidFrom == EventExpensePaymentSource.OutsideTin;

    public bool IsPaidFromBank => PaidFrom == EventExpensePaymentSource.Other;

    public string PaidFromText => PaidFrom switch
    {
        EventExpensePaymentSource.InsideTill => "Inside till",
        EventExpensePaymentSource.OutsideTin => "Outside tin",
        _ => "Paid from bank",
    };

    public IAsyncRelayCommand BeginDescriptionCommand { get; }

    public IAsyncRelayCommand BeginAmountCommand { get; }

    public IRelayCommand CyclePaidFromCommand { get; }

    public IRelayCommand SetPaidFromInsideCommand { get; }

    public IRelayCommand SetPaidFromOutsideCommand { get; }

    public IRelayCommand SetPaidFromBankCommand { get; }

    public EventExpenseRow ToModel() =>
        new()
        {
            Description = Description,
            Amount = Amount,
            PaidFrom = PaidFrom,
            Kind = Kind,
        };

    private void CyclePaidFrom() =>
        PaidFrom = Kind == EventExpenseKind.CashPrize
            ? PaidFrom == EventExpensePaymentSource.InsideTill
                ? EventExpensePaymentSource.OutsideTin
                : EventExpensePaymentSource.InsideTill
            : PaidFrom switch
            {
                EventExpensePaymentSource.Other => EventExpensePaymentSource.InsideTill,
                EventExpensePaymentSource.InsideTill => EventExpensePaymentSource.OutsideTin,
                _ => EventExpensePaymentSource.Other,
            };

    private async Task BeginDescriptionAsync()
    {
        var title = Kind == EventExpenseKind.CashPrize ? "Cash prize" : "Expense description";
        var r = await _input.ShowKeyboardAsync(Description, title, CancellationToken.None).ConfigureAwait(true);
        if (r is not null)
        {
            Description = r;
        }
    }

    private async Task BeginAmountAsync()
    {
        var title = Kind == EventExpenseKind.CashPrize ? "Cash prize amount" : "Expense amount";
        var r = await _input.ShowNumpadAsync(Amount, title, false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            Amount = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
    }
}

public sealed class MerchPrizeEditVm : ObservableViewModel
{
    private readonly IInputOverlayService _input;
    private readonly Action _onValuesChanged;

    private int _quantity;

    public MerchPrizeEditVm(IInputOverlayService input, long itemId, string itemName, Action onValuesChanged, int initialQty = 0)
    {
        _input = input;
        _onValuesChanged = onValuesChanged;
        ItemId = itemId;
        ItemName = itemName;
        _quantity = initialQty < 0 ? 0 : initialQty;
        BeginQtyCommand = new AsyncRelayCommand(BeginQtyAsync);
        IncrementQtyCommand = new RelayCommand(() => Quantity++);
        DecrementQtyCommand = new RelayCommand(() =>
        {
            if (Quantity > 0)
            {
                Quantity--;
            }
        });
    }

    public long ItemId { get; }

    public string ItemName { get; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value < 0 ? 0 : value))
            {
                OnPropertyChanged(nameof(QuantityText));
                _onValuesChanged();
            }
        }
    }

    public string QuantityText => _quantity.ToString(CultureInfo.InvariantCulture);

    public IAsyncRelayCommand BeginQtyCommand { get; }

    public IRelayCommand IncrementQtyCommand { get; }

    public IRelayCommand DecrementQtyCommand { get; }

    private async Task BeginQtyAsync()
    {
        var r = await _input.ShowIntegerNumpadAsync(Quantity, $"Prize qty — {ItemName}", 0, 999999, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            Quantity = r.Value;
        }
    }
}

public sealed class PitstopEndOfDayReportViewModel : ObservableViewModel
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly PitstopReportService _report;
    private readonly PitstopEodReconciliationService _pitstopReconciliation;
    private readonly ISquarePaymentReconciliationService _squareReconciliation;
    private readonly IPitstopRetailSaleRepository _pitstopSales;
    private readonly IPitstopEodBatchRepository _pitstopBatches;
    private readonly PitstopOutsideLineCatalogBuilder _outsideCatalog;
    private readonly IReportPathProvider _paths;
    private readonly IExportedFileLauncher _launcher;
    private readonly IUserSessionService _session;
    private readonly IStockEditingService _stock;
    private readonly IInputOverlayService _input;
    private readonly IWindowHandleProvider _windowHandle;
    private readonly IBackupService _backups;
    private readonly IAuditLogService _audit;
    private readonly PitstopSurchargeConfigLoader _surchargeConfig;
    private readonly INavigationService _navigation;
    private readonly PitstopEodCloseState _closeState = new();
    private readonly Dictionary<long, decimal?> _itemCosts = new();

    private readonly ObservableCollection<OutsideLineEditVm> _outsideLines = new();
    private CancellationTokenSource? _refreshDebounceCts;

    private string _eventName = "Pitstop";
    private DateTimeOffset _reportDate = DateTimeOffset.Now.Date;
    private SquarePaymentReconciliationResult? _squareReconciliationResult;
    private decimal? _manualCombinedSquareCardGross;
    private bool _isSquareManualMode;
    private decimal _squareFeePercent = 1.75m;
    private decimal _insideFloat;
    private decimal _outsideFloat;
    private decimal? _cashCounted;
    private decimal? _outsideCashCounted;
    private decimal? _floatRemoved;
    private string _archiveNotes = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private readonly MoneyActionLock _moneyLock = new();
    private bool _isRefreshing;
    private bool _isRefreshingSquare;
    private bool _hideZeroOutsideLines;
    private bool _showAdvancedDetails;
    private bool _showInsideItems;
    private decimal _cardSurchargePercent;
    private string? _lastExportedPdfPath;
    private bool _pitstopArchivedAfterCurrentExport;
    private bool _isTestMode;
    private PitstopReportData? _preview;

    private PitstopEodReconciliationReport? _pitstopReconciliationReport;

    public PitstopEndOfDayReportViewModel(
        PitstopReportService report,
        PitstopEodReconciliationService pitstopReconciliation,
        ISquarePaymentReconciliationService squareReconciliation,
        IPitstopRetailSaleRepository pitstopSales,
        IPitstopEodBatchRepository pitstopBatches,
        PitstopOutsideLineCatalogBuilder outsideCatalog,
        IReportPathProvider paths,
        IExportedFileLauncher launcher,
        IUserSessionService session,
        IStockEditingService stock,
        IInputOverlayService input,
        IWindowHandleProvider windowHandle,
        IBackupService backups,
        IAuditLogService audit,
        PitstopSurchargeConfigLoader surchargeConfig,
        INavigationService navigation)
    {
        _report = report;
        _pitstopReconciliation = pitstopReconciliation;
        _squareReconciliation = squareReconciliation;
        _pitstopSales = pitstopSales;
        _pitstopBatches = pitstopBatches;
        _outsideCatalog = outsideCatalog;
        _paths = paths;
        _launcher = launcher;
        _session = session;
        _stock = stock;
        _input = input;
        _windowHandle = windowHandle;
        _backups = backups;
        _audit = audit;
        _surchargeConfig = surchargeConfig;
        _navigation = navigation;

        MerchOutsideLines = new ObservableCollection<OutsideLineEditVm>();
        RaffleOutsideLines = new ObservableCollection<OutsideLineEditVm>();
        Expenses = new ObservableCollection<EventExpenseEditVm>();
        CashPrizes = new ObservableCollection<EventExpenseEditVm>();
        Prizes = new ObservableCollection<MerchPrizeEditVm>();

        AddExpenseCommand = new RelayCommand(AddExpense);
        AddCashPrizeCommand = new RelayCommand(AddCashPrize);
        AddStockPrizeCommand = new AsyncRelayCommand(AddStockPrizeAsync);
        RemoveExpenseCommand = new RelayCommand<EventExpenseEditVm>(RemoveExpense);
        RemoveCashPrizeCommand = new RelayCommand<EventExpenseEditVm>(RemoveCashPrize);
        RemovePrizeCommand = new RelayCommand<MerchPrizeEditVm>(RemovePrize);
        ClosePitstopCommand = new AsyncRelayCommand(ClosePitstopAsync, () => !IsBusy && Preview is not null);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync, () => !IsBusy && Preview is not null);
        RefreshSquareCommand = new AsyncRelayCommand(RefreshSquareAsync, () => !IsBusy && !IsRefreshingSquare);
        ReloadOutsideLinesFromCatalogCommand = new AsyncRelayCommand(ReloadOutsideLinesFromCatalogAsync, () => !IsBusy);
        ToggleHideZeroOutsideLinesCommand = new RelayCommand(ToggleHideZeroOutsideLines);
        ToggleAdvancedDetailsCommand = new RelayCommand(ToggleAdvancedDetails);
        ToggleInsideItemsCommand = new RelayCommand(ToggleInsideItems);
        LoadTestReportCommand = new AsyncRelayCommand(LoadTestReportAsync, () => !IsBusy && CanRunTestReport);
        ClearTestReportCommand = new AsyncRelayCommand(ClearTestReportAsync, () => IsTestMode);

        BeginEventNameCommand = new AsyncRelayCommand(BeginEventNameAsync);
        BeginReportDateCommand = new AsyncRelayCommand(BeginReportDateAsync);
        ShowPosSquareTransactionsCommand = new AsyncRelayCommand(ShowPosSquareTransactionsAsync);
        ShowOutsideSquareTransactionsCommand = new AsyncRelayCommand(ShowOutsideSquareTransactionsAsync);
        SetSquareAutoModeCommand = new RelayCommand(SetSquareAutoMode);
        SetSquareManualModeCommand = new RelayCommand(SetSquareManualMode);
        BeginManualCombinedSquareCommand = new AsyncRelayCommand(BeginManualCombinedSquareAsync);
        ClearManualCombinedSquareCommand = new RelayCommand(ClearManualCombinedSquare);
        BeginSquareFeeCommand = new AsyncRelayCommand(BeginSquareFeeAsync);
        BeginInsideFloatCommand = new AsyncRelayCommand(BeginInsideFloatAsync);
        BeginOutsideFloatCommand = new AsyncRelayCommand(BeginOutsideFloatAsync);
        BeginCashCountedCommand = new AsyncRelayCommand(BeginCashCountedAsync);
        BeginOutsideCashCountedCommand = new AsyncRelayCommand(BeginOutsideCashCountedAsync);
        BeginFloatRemovedCommand = new AsyncRelayCommand(BeginFloatRemovedAsync);
        BeginArchiveNotesCommand = new AsyncRelayCommand(BeginArchiveNotesAsync);
        ClearCashCountedCommand = new RelayCommand(() => CashCounted = null);
        ClearOutsideCashCountedCommand = new RelayCommand(() => OutsideCashCounted = null);
        ClearFloatRemovedCommand = new RelayCommand(() => FloatRemoved = null);
    }

    public ObservableCollection<OutsideLineEditVm> MerchOutsideLines { get; }

    public ObservableCollection<OutsideLineEditVm> RaffleOutsideLines { get; }

    public ObservableCollection<EventExpenseEditVm> Expenses { get; }

    public ObservableCollection<EventExpenseEditVm> CashPrizes { get; }

    public ObservableCollection<MerchPrizeEditVm> Prizes { get; }

    public IRelayCommand AddExpenseCommand { get; }

    public IRelayCommand AddCashPrizeCommand { get; }

    public IAsyncRelayCommand AddStockPrizeCommand { get; }

    public IRelayCommand<EventExpenseEditVm> RemoveExpenseCommand { get; }

    public IRelayCommand<EventExpenseEditVm> RemoveCashPrizeCommand { get; }

    public IRelayCommand<MerchPrizeEditVm> RemovePrizeCommand { get; }

    public IAsyncRelayCommand ClosePitstopCommand { get; }

    public IAsyncRelayCommand ExportPdfCommand { get; }

    public IAsyncRelayCommand RefreshSquareCommand { get; }

    public IAsyncRelayCommand ReloadOutsideLinesFromCatalogCommand { get; }

    public IRelayCommand ToggleHideZeroOutsideLinesCommand { get; }

    public IRelayCommand ToggleAdvancedDetailsCommand { get; }

    public IRelayCommand ToggleInsideItemsCommand { get; }

    public IAsyncRelayCommand LoadTestReportCommand { get; }

    public IAsyncRelayCommand ClearTestReportCommand { get; }

    public bool CanRunTestReport => _session.IsDeveloper;

    public bool IsTestMode
    {
        get => _isTestMode;
        private set
        {
            if (SetProperty(ref _isTestMode, value))
            {
                OnPropertyChanged(nameof(IsTestModeBannerVisible));
                OnPropertyChanged(nameof(TestModeBannerText));
                LoadTestReportCommand.NotifyCanExecuteChanged();
                ClearTestReportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsTestModeBannerVisible => IsTestMode;

    public string TestModeBannerText =>
        "TEST MODE - sample terminal sales and figures. Export is watermarked and archive is disabled.";

    public IAsyncRelayCommand BeginEventNameCommand { get; }

    public IAsyncRelayCommand BeginReportDateCommand { get; }

    public IAsyncRelayCommand ShowPosSquareTransactionsCommand { get; }

    public IAsyncRelayCommand ShowOutsideSquareTransactionsCommand { get; }

    public IRelayCommand SetSquareAutoModeCommand { get; }

    public IRelayCommand SetSquareManualModeCommand { get; }

    public IAsyncRelayCommand BeginManualCombinedSquareCommand { get; }

    public IRelayCommand ClearManualCombinedSquareCommand { get; }

    public IAsyncRelayCommand BeginSquareFeeCommand { get; }

    public IAsyncRelayCommand BeginInsideFloatCommand { get; }

    public IAsyncRelayCommand BeginOutsideFloatCommand { get; }

    public IAsyncRelayCommand BeginCashCountedCommand { get; }

    public IAsyncRelayCommand BeginOutsideCashCountedCommand { get; }

    public IAsyncRelayCommand BeginFloatRemovedCommand { get; }

    public IAsyncRelayCommand BeginArchiveNotesCommand { get; }

    public IRelayCommand ClearCashCountedCommand { get; }

    public IRelayCommand ClearOutsideCashCountedCommand { get; }

    public IRelayCommand ClearFloatRemovedCommand { get; }

    public string EventName
    {
        get => _eventName;
        set
        {
            if (SetProperty(ref _eventName, value ?? string.Empty))
            {
                ScheduleRefresh();
            }
        }
    }

    public DateTimeOffset ReportDate
    {
        get => _reportDate;
        set
        {
            if (SetProperty(ref _reportDate, value))
            {
                OnPropertyChanged(nameof(ReportPeriodCaption));
                OnPropertyChanged(nameof(HeaderSubtitle));
                ResetExportReadyState();
                _ = RefreshSquareAndPreviewAsync();
            }
        }
    }

    public string StaffDisplay =>
        string.IsNullOrWhiteSpace(_session.DisplayName) ? "\u2014" : _session.DisplayName.Trim();

    public string HeaderSubtitle =>
        $"{ReportDate.Date:d MMM yyyy}  •  {StaffDisplay}";

    public bool ShowInsideItems
    {
        get => _showInsideItems;
        private set
        {
            if (SetProperty(ref _showInsideItems, value))
            {
                OnPropertyChanged(nameof(InsideItemsToggleLabel));
            }
        }
    }

    public string InsideItemsToggleLabel => ShowInsideItems ? "Hide items" : "View items";

    private void ToggleInsideItems() => ShowInsideItems = !ShowInsideItems;

    public string ReportPeriodCaption
    {
        get
        {
            var day = ReportDate.Date;
            return $"{day:dddd d MMMM yyyy}";
        }
    }

    public string PosSquareGrossText => Preview is null ? "\u2014" : Money(Preview.PosSquareGross);

    public string OutsideSquareGrossText => Preview is null ? "\u2014" : Money(Preview.OutsideSquareGross);

    public string CombinedSquareGrossText => Preview is null ? "\u2014" : Money(Preview.CombinedSquareCardGross);

    public bool IsSquareManualMode
    {
        get => _isSquareManualMode;
        private set
        {
            if (SetProperty(ref _isSquareManualMode, value))
            {
                OnPropertyChanged(nameof(IsSquareAutoMode));
                OnPropertyChanged(nameof(SquareModeHelpText));
                OnPropertyChanged(nameof(OutsideSalesHelpText));
                OnPropertyChanged(nameof(RefreshSquareButtonLabel));
                OnPropertyChanged(nameof(SquareReconciliationStatusText));
                OnPropertyChanged(nameof(ManualCardFallbackStatusText));
            }
        }
    }

    public bool IsSquareAutoMode => !IsSquareManualMode;

    public string SquareModeHelpText =>
        IsSquareManualMode
            ? "Square won’t load? Enter the day’s total card sales from Square. Merch card sales are worked out automatically."
            : "Card totals come from Square automatically. Tap a total to see the receipts.";

    public string OutsideSalesHelpText =>
        IsSquareManualMode
            ? "Type the cash amounts from the merch paper sheet. If you sold merch on card, also enter those qty / $ for stock."
            : "Type the cash amounts from the merch paper sheet. Card merch sales are filled in from Square.";

    public string RefreshSquareButtonLabel =>
        IsSquareManualMode ? "Refresh card (till)" : "Refresh card sales";

    public bool ShowAdvancedDetails
    {
        get => _showAdvancedDetails;
        private set
        {
            if (SetProperty(ref _showAdvancedDetails, value))
            {
                OnPropertyChanged(nameof(ShowAdvancedDetailsLabel));
            }
        }
    }

    public string ShowAdvancedDetailsLabel =>
        ShowAdvancedDetails ? "Hide extra details" : "Show extra details";

    private void ToggleAdvancedDetails() => ShowAdvancedDetails = !ShowAdvancedDetails;

    public bool HasManualCombinedSquareCardGross =>
        _manualCombinedSquareCardGross is decimal value && value > 0m;

    public string ManualCombinedSquareCardGrossText =>
        HasManualCombinedSquareCardGross
            ? Money(_manualCombinedSquareCardGross!.Value)
            : "Tap to enter total card sales";

    public string ManualOutsideDerivedText
    {
        get
        {
            if (!HasManualCombinedSquareCardGross || Preview is null)
            {
                return "\u2014";
            }

            return Money(Preview.OutsideSquareGross);
        }
    }

    public string ManualCardFallbackStatusText
    {
        get
        {
            if (!IsSquareManualMode)
            {
                return string.Empty;
            }

            if (!HasManualCombinedSquareCardGross)
            {
                return "Enter the total card sales for the day from Square (till + merch).";
            }

            return Preview?.UsingManualSquareCardFallback == true
                ? "Using your entered total. Merch card = that total minus till card sales."
                : string.Empty;
        }
    }

    public string PosSquareTransactionCountText =>
        Preview is null ? "\u2014" : Preview.PosSquareTransactionCount.ToString(Inv);

    public string OutsideSquareTransactionCountText =>
        Preview is null ? "\u2014" : Preview.OutsideSquareTransactionCount.ToString(Inv);

    public string SquareFeesText =>
        Preview is null
            ? "\u2014"
            : Preview.ActualSquareFees is decimal f
                ? Money(f)
                : $"{Money(Preview.EstimatedSquareFees)} (est.)";

    public string ExpectedSquareDepositText =>
        Preview is null ? "\u2014" : Money(Preview.ExpectedSquareDeposit);

    public string SquareReconciliationStatusText
    {
        get
        {
            if (IsRefreshingSquare)
            {
                return IsSquareManualMode ? "Loading till card sales…" : "Loading card sales from Square…";
            }

            if (_squareReconciliationResult is null)
            {
                return "Card sales not loaded yet.";
            }

            if (!string.IsNullOrWhiteSpace(_squareReconciliationResult.LoadError)
                && !IsSquareManualMode)
            {
                return $"{_squareReconciliationResult.LoadError}";
            }

            if (IsSquareManualMode)
            {
                if (!string.IsNullOrWhiteSpace(_squareReconciliationResult.LoadError)
                    && _squareReconciliationResult.MatchedPayments.Count == 0)
                {
                    return _squareReconciliationResult.LoadError;
                }

                return HasManualCombinedSquareCardGross
                    ? "Manual card total entered."
                    : "Enter the card total from Square.";
            }

            var excluded = _squareReconciliationResult.ExcludedNonPitstopTransactionCount;
            var excludedPart = excluded > 0
                ? $" ({excluded} non-Pitstop payments skipped)"
                : string.Empty;

            return $"Loaded — {Preview?.PosSquareTransactionCount ?? 0} till, {Preview?.OutsideSquareTransactionCount ?? 0} merch{excludedPart}.";
        }
    }

    public bool NeedsManualOutsideCard =>
        IsSquareManualMode
        || (!IsRefreshingSquare
            && _squareReconciliationResult is not null
            && !string.IsNullOrWhiteSpace(_squareReconciliationResult.LoadError)
            && !(_squareReconciliationResult.LoadedFromSquare));

    public string EnterCardManuallyLabel =>
        HasManualCombinedSquareCardGross
            ? $"Card total {ManualCombinedSquareCardGrossText}"
            : "Enter card total manually";

    public bool HasPitstopProductSales => Preview?.PitstopProductSales.Count > 0;

    public IReadOnlyList<PitstopProductAggregateRow> PitstopProductSales =>
        Preview?.PitstopProductSales ?? Array.Empty<PitstopProductAggregateRow>();

    public string PitstopItemsSoldCaption
    {
        get
        {
            var quantity = Preview?.PitstopProductSales.Sum(p => p.Quantity) ?? 0;
            return quantity == 1 ? "1 item sold" : $"{quantity} items sold";
        }
    }

    public bool HasOutsideTerminalProductSales =>
        Preview?.OutsideTerminalProductSales.Count > 0;

    public IReadOnlyList<PitstopProductAggregateRow> OutsideTerminalProductSales =>
        Preview?.OutsideTerminalProductSales ?? Array.Empty<PitstopProductAggregateRow>();

    public IReadOnlyList<PitstopCategoryAggregateRow> OutsideTerminalCategorySales =>
        Preview?.OutsideTerminalCategorySales ?? Array.Empty<PitstopCategoryAggregateRow>();

    public bool HasCombinedOutsideSales => Preview?.CombinedOutsideSales.Count > 0;

    public IReadOnlyList<CombinedOutsideSaleRow> CombinedOutsideSales =>
        Preview?.CombinedOutsideSales ?? Array.Empty<CombinedOutsideSaleRow>();

    public IReadOnlyList<EventCategoryComparisonRow> EventCategoryComparison =>
        Preview?.EventCategoryComparison ?? Array.Empty<EventCategoryComparisonRow>();

    public decimal SquareFeePercent
    {
        get => _squareFeePercent;
        set
        {
            if (SetProperty(ref _squareFeePercent, value))
            {
                OnPropertyChanged(nameof(SquareFeePercentText));
                ScheduleRefresh();
            }
        }
    }

    public string SquareFeePercentText => _squareFeePercent.ToString("0.00", Inv);

    public decimal InsideFloat
    {
        get => _insideFloat;
        set
        {
            if (SetProperty(ref _insideFloat, value))
            {
                OnPropertyChanged(nameof(InsideFloatText));
                OnPropertyChanged(nameof(ExpectedCash));
                OnPropertyChanged(nameof(ExpectedCashText));
                OnPropertyChanged(nameof(CashVariance));
                OnPropertyChanged(nameof(CashVarianceText));
                OnPropertyChanged(nameof(HasCashVariance));
                ScheduleRefresh();
            }
        }
    }

    public string InsideFloatText => Money(InsideFloat);

    public decimal OutsideFloat
    {
        get => _outsideFloat;
        set
        {
            if (SetProperty(ref _outsideFloat, value))
            {
                OnPropertyChanged(nameof(OutsideFloatText));
                ScheduleRefresh();
            }
        }
    }

    public string OutsideFloatText => Money(OutsideFloat);

    public decimal? CashCounted
    {
        get => _cashCounted;
        set
        {
            if (SetProperty(ref _cashCounted, value))
            {
                OnPropertyChanged(nameof(CashCountedText));
                OnPropertyChanged(nameof(InsideCountedButtonText));
                OnPropertyChanged(nameof(ExpectedCashText));
                OnPropertyChanged(nameof(CashVarianceText));
                OnPropertyChanged(nameof(HasCashVariance));
                OnPropertyChanged(nameof(HasInsideVariance));
                OnPropertyChanged(nameof(InsideIsBalanced));
                OnPropertyChanged(nameof(InsideVarianceStatusText));
                OnPropertyChanged(nameof(ShowInsideNoteAction));
                ScheduleRefresh();
            }
        }
    }

    public string CashCountedText => CashCounted is null ? "—" : Money(CashCounted.Value);

    public decimal? OutsideCashCounted
    {
        get => _outsideCashCounted;
        set
        {
            if (SetProperty(ref _outsideCashCounted, value))
            {
                OnPropertyChanged(nameof(OutsideCashCountedText));
                OnPropertyChanged(nameof(OutsideCountedButtonText));
                OnPropertyChanged(nameof(HasOutsideVariance));
                OnPropertyChanged(nameof(OutsideIsBalanced));
                OnPropertyChanged(nameof(OutsideVarianceStatusText));
                OnPropertyChanged(nameof(ShowInsideNoteAction));
                ScheduleRefresh();
            }
        }
    }

    public string OutsideCashCountedText => OutsideCashCounted is null ? "—" : Money(OutsideCashCounted.Value);

    public decimal? FloatRemoved
    {
        get => _floatRemoved;
        set
        {
            if (SetProperty(ref _floatRemoved, value))
            {
                OnPropertyChanged(nameof(FloatRemovedText));
                ScheduleRefresh();
            }
        }
    }

    public string FloatRemovedText => FloatRemoved is null ? "—" : Money(FloatRemoved.Value);

    public decimal? ExpectedCash => GetTill("inside")?.Expected;

    public string ExpectedCashText => ExpectedCash is null ? "—" : Money(ExpectedCash.Value);

    public decimal? CashVariance => GetTill("inside")?.Variance;

    public string CashVarianceText => CashVariance is null ? "—" : Money(CashVariance.Value);

    public bool HasCashVariance => CashVariance is decimal v && v != 0m;

    public decimal? OutsideExpectedCash => GetTill("outside")?.Expected;

    public string OutsideExpectedCashText => OutsideExpectedCash is null ? "—" : Money(OutsideExpectedCash.Value);

    public decimal? OutsideCashVariance => GetTill("outside")?.Variance;

    public string OutsideCashVarianceText => OutsideCashVariance is null ? "—" : Money(OutsideCashVariance.Value);

    private PitstopTillReconciliation? GetTill(string key) =>
        Preview?.TillReconciliations.FirstOrDefault(t => string.Equals(t.TillKey, key, StringComparison.Ordinal));

    public string ArchiveNotes
    {
        get => _archiveNotes;
        set
        {
            if (SetProperty(ref _archiveNotes, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(ArchiveNotesDisplay));
            }
        }
    }

    public string ArchiveNotesDisplay =>
        string.IsNullOrWhiteSpace(_archiveNotes) ? "(no notes)" : _archiveNotes;

    public bool HideZeroOutsideLines
    {
        get => _hideZeroOutsideLines;
        set
        {
            if (SetProperty(ref _hideZeroOutsideLines, value))
            {
                OnPropertyChanged(nameof(HideZeroOutsideLinesLabel));
                RebuildOutsideGroups();
            }
        }
    }

    public string HideZeroOutsideLinesLabel =>
        HideZeroOutsideLines ? "Show all items" : "Hide unused items";

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
                ExportPdfCommand.NotifyCanExecuteChanged();
                ClosePitstopCommand.NotifyCanExecuteChanged();
                ReloadOutsideLinesFromCatalogCommand.NotifyCanExecuteChanged();
                LoadTestReportCommand.NotifyCanExecuteChanged();
                AddStockPrizeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public bool IsRefreshingSquare
    {
        get => _isRefreshingSquare;
        private set
        {
            if (SetProperty(ref _isRefreshingSquare, value))
            {
                RefreshSquareCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(SquareReconciliationStatusText));
                OnPropertyChanged(nameof(ManualOutsideDerivedText));
                OnPropertyChanged(nameof(ManualCardFallbackStatusText));
            }
        }
    }

    public PitstopReportData? Preview
    {
        get => _preview;
        private set
        {
            if (SetProperty(ref _preview, value))
            {
                ExportPdfCommand.NotifyCanExecuteChanged();
                ClosePitstopCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(HasPreview));
                OnPropertyChanged(nameof(HasCardMismatch));
                OnPropertyChanged(nameof(MismatchBannerText));
                OnPropertyChanged(nameof(CashToDepositText));
                OnPropertyChanged(nameof(NetProfitText));
                OnPropertyChanged(nameof(GrossSalesText));
                OnPropertyChanged(nameof(PitstopCashText));
                OnPropertyChanged(nameof(PitstopCardText));
                OnPropertyChanged(nameof(OutsideCashText));
                OnPropertyChanged(nameof(OutsideCardText));
                OnPropertyChanged(nameof(PosSquareGrossText));
                OnPropertyChanged(nameof(OutsideSquareGrossText));
                OnPropertyChanged(nameof(CombinedSquareGrossText));
                OnPropertyChanged(nameof(ManualOutsideDerivedText));
                OnPropertyChanged(nameof(ManualCardFallbackStatusText));
                OnPropertyChanged(nameof(PosSquareTransactionCountText));
                OnPropertyChanged(nameof(OutsideSquareTransactionCountText));
                OnPropertyChanged(nameof(SquareFeesText));
                OnPropertyChanged(nameof(ExpectedSquareDepositText));
                OnPropertyChanged(nameof(SquareReconciliationStatusText));
                OnPropertyChanged(nameof(HasPitstopProductSales));
                OnPropertyChanged(nameof(PitstopProductSales));
                OnPropertyChanged(nameof(PitstopItemsSoldCaption));
                OnPropertyChanged(nameof(HasOutsideTerminalProductSales));
                OnPropertyChanged(nameof(OutsideTerminalProductSales));
                OnPropertyChanged(nameof(OutsideTerminalCategorySales));
                OnPropertyChanged(nameof(HasCombinedOutsideSales));
                OnPropertyChanged(nameof(CombinedOutsideSales));
                OnPropertyChanged(nameof(EventCategoryComparison));
                OnPropertyChanged(nameof(SquareBatchText));
                OnPropertyChanged(nameof(SquareTerminalDiffText));
                OnPropertyChanged(nameof(LastRefreshedText));
                OnPropertyChanged(nameof(ExpectedCash));
                OnPropertyChanged(nameof(ExpectedCashText));
                OnPropertyChanged(nameof(CashVariance));
                OnPropertyChanged(nameof(CashVarianceText));
                OnPropertyChanged(nameof(HasCashVariance));
                OnPropertyChanged(nameof(OutsideExpectedCash));
                OnPropertyChanged(nameof(OutsideExpectedCashText));
                OnPropertyChanged(nameof(OutsideCashVariance));
                OnPropertyChanged(nameof(OutsideCashVarianceText));
                OnPropertyChanged(nameof(InsideTotalSalesText));
                OnPropertyChanged(nameof(OutsideItemSalesText));
                OnPropertyChanged(nameof(OutsideCardSalesText));
                OnPropertyChanged(nameof(OutsideCashSalesText));
                OnPropertyChanged(nameof(HasOutsideSurcharge));
                OnPropertyChanged(nameof(OutsideSurchargeText));
                OnPropertyChanged(nameof(OutsideSquareReceivedText));
                OnPropertyChanged(nameof(NeedsManualOutsideCard));
                OnPropertyChanged(nameof(EnterCardManuallyLabel));
                OnPropertyChanged(nameof(InsideVarianceStatusText));
                OnPropertyChanged(nameof(OutsideVarianceStatusText));
                OnPropertyChanged(nameof(InsideIsBalanced));
                OnPropertyChanged(nameof(OutsideIsBalanced));
                OnPropertyChanged(nameof(HasInsideVariance));
                OnPropertyChanged(nameof(HasOutsideVariance));
                OnPropertyChanged(nameof(InsideCountedButtonText));
                OnPropertyChanged(nameof(OutsideCountedButtonText));
                OnPropertyChanged(nameof(InsideCashSalesText));
                OnPropertyChanged(nameof(InsidePaidOutText));
                OnPropertyChanged(nameof(OutsidePaidOutText));
                OnPropertyChanged(nameof(TotalCashSalesText));
                OnPropertyChanged(nameof(TotalCardSalesText));
                OnPropertyChanged(nameof(CashPrizesText));
                OnPropertyChanged(nameof(ExpensesText));
                OnPropertyChanged(nameof(KnownStockCostsText));
                OnPropertyChanged(nameof(HasUnknownStockCosts));
                OnPropertyChanged(nameof(UnknownStockCostsNote));
                OnPropertyChanged(nameof(HasCashPrizes));
                OnPropertyChanged(nameof(HasExpenses));
                OnPropertyChanged(nameof(HasStockPrizes));
            }
        }
    }

    public bool HasPreview => Preview is not null;

    public bool HasCardMismatch => Preview?.OutsideCardMismatch == true;

    public string MismatchBannerText
    {
        get
        {
            if (Preview is null || !Preview.OutsideCardMismatch)
            {
                return string.Empty;
            }

            return
                $"Check the card total — Square and the entered sales don’t line up "
                + $"(difference ${Money(Math.Abs(Preview.OutsideCardDifference))}).";
        }
    }

    public string CashToDepositText => Preview is null ? "\u2014" : Money(Preview.CashToDeposit);

    public string NetProfitText => Preview is null ? "\u2014" : Money(Preview.NetEventProfit);

    public string GrossSalesText => Preview is null ? "\u2014" : Money(Preview.GrossSales);

    public string PitstopCashText => Preview is null ? "\u2014" : Money(Preview.PitstopRetailCash);

    public string PitstopCardText => Preview is null ? "\u2014" : Money(Preview.PitstopCardBaseProductTotal);

    public string InsideTotalSalesText =>
        Preview is null
            ? "\u2014"
            : Money(Preview.PitstopRetailCash + Preview.PitstopCardBaseProductTotal);

    public string OutsideCashText => Preview is null ? "\u2014" : Money(Preview.OutsideCashTotal);

    public string OutsideCardText => Preview is null ? "\u2014" : Money(Preview.OutsideCardSales);

    public string OutsideItemSalesText => Preview is null ? "\u2014" : Money(Preview.OutsideItemSalesTotal);

    public string OutsideCardSalesText => Preview is null ? "\u2014" : Money(Preview.OutsideCardSales);

    public string OutsideCashSalesText => Preview is null ? "\u2014" : Money(Preview.OutsideCashTotal);

    public bool HasOutsideSurcharge => Preview is { OutsideCardSurchargeCollected: > 0m };

    public string OutsideSurchargeText =>
        Preview is null ? "\u2014" : Money(Preview.OutsideCardSurchargeCollected);

    public string OutsideSquareReceivedText => Preview is null ? "\u2014" : Money(Preview.OutsideSquareGross);

    public string SquareBatchText => Preview is null ? "\u2014" : Money(Preview.CombinedSquareCardGross);

    public string SquareTerminalDiffText => Preview is null ? "\u2014" : Money(Preview.OutsideCardDifference);

    public string LastRefreshedText =>
        Preview is null ? "Not calculated yet" : $"Updated {DateTime.Now:HH:mm}";

    public string InsideCashSalesText => PitstopCashText;

    public string InsidePaidOutText =>
        Preview is null
            ? "\u2014"
            : Money(GetTill("inside")?.CashPaidOut ?? 0m);

    public string OutsidePaidOutText =>
        Preview is null
            ? "\u2014"
            : Money(GetTill("outside")?.CashPaidOut ?? 0m);

    public string InsideCountedButtonText =>
        CashCounted is null ? "Counted: tap to enter" : $"Counted: ${Money(CashCounted.Value)}";

    public string OutsideCountedButtonText =>
        OutsideCashCounted is null ? "Counted: tap to enter" : $"Counted: ${Money(OutsideCashCounted.Value)}";

    public string InsideVarianceStatusText =>
        PitstopEodCalculator.VarianceStatus(CashVariance);

    public string OutsideVarianceStatusText =>
        PitstopEodCalculator.VarianceStatus(OutsideCashVariance);

    public bool InsideIsBalanced => PitstopEodCalculator.IsBalanced(CashVariance);

    public bool OutsideIsBalanced => PitstopEodCalculator.IsBalanced(OutsideCashVariance);

    public bool HasInsideVariance => CashVariance is decimal v && v != 0m;

    public bool HasOutsideVariance => OutsideCashVariance is decimal v && v != 0m;

    public bool ShowInsideNoteAction => HasInsideVariance || HasOutsideVariance;

    public string TotalCashSalesText => Preview is null ? "\u2014" : Money(Preview.TotalCashGross);

    public string TotalCardSalesText => Preview is null ? "\u2014" : Money(Preview.TotalCardGross);

    public string CashPrizesText => Preview is null ? "\u2014" : Money(Preview.TotalCashPrizes);

    public string ExpensesText => Preview is null ? "\u2014" : Money(Preview.TotalExpenses);

    public string KnownStockCostsText => Preview is null ? "\u2014" : Money(Preview.KnownStockCosts);

    public bool HasUnknownStockCosts => Preview?.HasUnknownStockCosts == true;

    public string UnknownStockCostsNote =>
        HasUnknownStockCosts ? "Some stock costs are unknown." : string.Empty;

    public bool HasCashPrizes => CashPrizes.Count > 0;

    public bool HasExpenses => Expenses.Count > 0;

    public bool HasStockPrizes => Prizes.Count > 0;

    public string CloseoutChecklistText =>
        "1. Check sales\n2. Add prizes or expenses if needed\n3. Count cash\n4. Close Pitstop";

    public string ReconciliationSummaryText =>
        _pitstopReconciliationReport is null
            ? string.Empty
            : $"Till today — cash ${_pitstopReconciliationReport.CashSales:0.00}, "
              + $"card ${_pitstopReconciliationReport.CardSalesCharged:0.00}.";

    public ObservableCollection<string> ReconciliationWarnings { get; } = new();

    public async Task InitializeAsync()
    {
        try
        {
            _cardSurchargePercent = await _surchargeConfig
                .LoadCardSurchargePercentAsync(CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch
        {
            _cardSurchargePercent = 0m;
        }

        await LoadItemCostsAsync().ConfigureAwait(true);

        var hasStaleOutsideLines = _outsideLines.Any(l => !l.IsMerch && !l.IsRaffle);
        if (_outsideLines.Count == 0 || hasStaleOutsideLines)
        {
            await PopulateOutsideLinesFromCatalogAsync(preserveExistingKeys: false).ConfigureAwait(true);
        }
        else
        {
            RebuildOutsideGroups();
        }

        await RefreshSquareAndPreviewAsync().ConfigureAwait(true);
    }

    private async Task RefreshSquareAndPreviewAsync(CancellationToken cancellationToken = default)
    {
        await RefreshSquareAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task RefreshSquareAsync(CancellationToken cancellationToken = default)
    {
        IsRefreshingSquare = true;
        try
        {
            var day = ReportDate.Date;
            var start = new DateTimeOffset(day, ReportDate.Offset);
            var end = start.AddDays(1);

            _squareReconciliationResult = IsTestMode
                ? PitstopReportTestDataBuilder.BuildSquareReconciliation(SquareFeePercent)
                : await _squareReconciliation
                    .ReconcileAsync(
                        start,
                        end,
                        SquareFeePercent,
                        cancellationToken,
                        includeOutsideTerminal: !IsSquareManualMode)
                    .ConfigureAwait(true);

            if (IsSquareManualMode && _squareReconciliationResult is not null)
            {
                _squareReconciliationResult =
                    SquarePaymentReconciliationService.WithoutOutsideTerminal(_squareReconciliationResult);
            }
            OnPropertyChanged(nameof(SquareReconciliationStatusText));
        }
        catch (Exception ex)
        {
            _squareReconciliationResult = SquarePaymentReconciliationResult.Empty(ex.Message);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsRefreshingSquare = false;
        }

        await RefreshPreviewAsync(cancellationToken).ConfigureAwait(true);
    }

    private void OnInputChanged()
    {
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        _refreshDebounceCts?.Cancel();
        _refreshDebounceCts = new CancellationTokenSource();
        var token = _refreshDebounceCts.Token;
        _ = DebouncedRefreshAsync(token);
    }

    private async Task DebouncedRefreshAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token).ConfigureAwait(false);
            await RefreshPreviewAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // superseded
        }
    }

    private async Task RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        IsRefreshing = true;
        try
        {
            var inputs = BuildInputs();
            var data = await _report.BuildAsync(inputs, cancellationToken).ConfigureAwait(true);
            _pitstopReconciliationReport = IsTestMode
                ? PitstopReportTestDataBuilder.BuildReconciliationReport(
                    inputs.PeriodStartLocal,
                    inputs.PeriodEndLocal,
                    SquareFeePercent)
                : await _pitstopReconciliation
                    .BuildAsync(inputs.PeriodStartLocal, inputs.PeriodEndLocal, SquareFeePercent, cancellationToken)
                    .ConfigureAwait(true);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Preview = data;
            ReconciliationWarnings.Clear();
            foreach (var w in _pitstopReconciliationReport.Warnings)
            {
                ReconciliationWarnings.Add(w);
            }

            foreach (var w in data.Warnings)
            {
                if (!string.IsNullOrWhiteSpace(w) && !ReconciliationWarnings.Contains(w))
                {
                    ReconciliationWarnings.Add(w);
                }
            }

            foreach (var till in data.TillReconciliations.Where(t => t.Variance is not null))
            {
                var variance = till.Variance!.Value;
                if (Math.Abs(variance) >= 0.01m)
                {
                    var sign = variance > 0 ? "over" : "short";
                    ReconciliationWarnings.Add(
                        $"{till.TillLabel} is {sign} by {Math.Abs(variance):C2} "
                        + $"(counted {till.Counted:C2} vs expected {till.Expected:C2}).");
                }
            }

            OnPropertyChanged(nameof(ReconciliationSummaryText));
            StatusMessage = data.OutsideCardMismatch
                ? "Square reconciliation mismatch — review before export."
                : IsTestMode
                    ? "Test report loaded — sample data only. Save and Export to preview the PDF."
                    : ReconciliationWarnings.Count > 0
                        ? $"Pitstop totals updated — {ReconciliationWarnings.Count} reconciliation warning(s)."
                        : "Pitstop totals are up to date (bar tabs excluded).";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private PitstopReportInputs BuildInputs()
    {
        var day = ReportDate.Date;
        var start = new DateTimeOffset(day, ReportDate.Offset);
        var inputs = new PitstopReportInputs
        {
            EventName = EventName,
            PeriodStartLocal = start,
            PeriodEndLocal = start.AddDays(1),
            StaffName = StaffDisplay is "\u2014" ? null : StaffDisplay,
            SquareReconciliation = _squareReconciliationResult,
            UseManualSquareCardMode = IsSquareManualMode,
            ManualCombinedSquareCardGross = IsSquareManualMode ? _manualCombinedSquareCardGross : null,
            SquareFeePercent = SquareFeePercent,
            CardSurchargePercent = _cardSurchargePercent,
            InsideFloat = InsideFloat,
            OutsideFloat = OutsideFloat,
            CashCounted = CashCounted,
            OutsideCashCounted = OutsideCashCounted,
            FloatRemoved = FloatRemoved,
            UseTestPosData = IsTestMode,
        };

        foreach (var o in _outsideLines)
        {
            inputs.OutsideLines.Add(o.ToModel());
        }

        foreach (var e in Expenses.Concat(CashPrizes))
        {
            inputs.Expenses.Add(e.ToModel());
        }

        foreach (var p in Prizes.Where(x => x.Quantity > 0))
        {
            inputs.PrizeGiveaways.Add(new MerchPrizeGiveawayRow { ItemId = p.ItemId, ItemName = p.ItemName, Quantity = p.Quantity });
        }

        try
        {
            foreach (var row in _itemCosts)
            {
                inputs.ItemUnitCosts[row.Key] = row.Value;
            }
        }
        catch
        {
            // Cost lookup is best-effort; unknown costs stay unknown.
        }

        return inputs;
    }

    private async Task ExportPdfAsync()
    {
        await ClosePitstopAsync().ConfigureAwait(true);
    }

    private async Task ClosePitstopAsync()
    {
        if (!_moneyLock.TryBegin())
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            _refreshDebounceCts?.Cancel();
            await LoadItemCostsAsync().ConfigureAwait(true);
            await RefreshPreviewAsync(CancellationToken.None).ConfigureAwait(true);
            if (Preview is null)
            {
                return;
            }

            if (Preview.OutsideCardMismatch)
            {
                if (!await ConfirmMismatchExportAsync().ConfigureAwait(true))
                {
                    StatusMessage = "Close cancelled — check card sales, then try again.";
                    return;
                }
            }
            var next = _closeState.Next;
            if (next == PitstopEodCloseCheckpoint.SavePdf || !_closeState.PdfSaved)
            {
                var saved = await SavePdfAsync().ConfigureAwait(true);
                if (!saved)
                {
                    return;
                }

                _closeState.PdfSaved = true;
                _closeState.PdfPath = _lastExportedPdfPath;
            }

            if (IsTestMode)
            {
                _closeState.SkipArchive = true;
                _closeState.SkipStock = true;
                StatusMessage = "Test report saved. Archive is disabled for sample data.";
                _closeState.Reset();
                return;
            }

            if (_closeState.Next == PitstopEodCloseCheckpoint.Archive)
            {
                var archived = await ArchiveAfterPdfAsync().ConfigureAwait(true);
                if (!archived)
                {
                    return;
                }
            }

            if (_closeState.Next == PitstopEodCloseCheckpoint.ApplyStock)
            {
                var stockOk = await ApplyStockAfterArchiveAsync().ConfigureAwait(true);
                if (!stockOk)
                {
                    return;
                }
            }

            StatusMessage = "Pitstop closed and report saved.";
            await ResetPitstopEodFormFieldsAsync().ConfigureAwait(true);
            ResetExportReadyState();
            _closeState.Reset();
            _navigation.Navigate(typeof(ReportsHomePage));
        }
        catch (Exception ex)
        {
            _closeState.LastFailure = ex.Message;
            StatusMessage = $"Close failed: {ex.Message}. You can tap Close again to retry.";
        }
        finally
        {
            IsBusy = false;
            _moneyLock.End();
        }
    }

    private async Task<bool> SavePdfAsync()
    {
        try
        {
            var bytes = PitstopReportPdfExporter.Build(Preview!);
            var dir = _paths.GetPitstopReportsDirectory();
            Directory.CreateDirectory(dir);
            var safe = string.Join("_", (EventName ?? "pitstop").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrEmpty(safe))
            {
                safe = "pitstop";
            }

            var fn = IsTestMode
                ? $"TEST_{safe}_{ReportDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf"
                : $"{safe}_{ReportDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf";
            var path = Path.Combine(dir, fn);
            await File.WriteAllBytesAsync(path, bytes, CancellationToken.None).ConfigureAwait(true);
            _lastExportedPdfPath = path;
            _pitstopArchivedAfterCurrentExport = false;

            try
            {
                await _audit.LogAsync(
                    AuditActions.PitstopEodExported,
                    AuditEntityTypes.PitstopEodBatch,
                    entityId: path,
                    amount: Preview!.GrossSales,
                    reason: $"EOD PDF exported for {ReportPeriodCaption}.").ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }

            _ = _launcher.TryLaunch(path);
            StatusMessage = "Report saved.";
            return true;
        }
        catch (Exception ex)
        {
            _closeState.LastFailure = ex.Message;
            StatusMessage = $"Could not save the report: {ex.Message}. Tap Close to retry.";
            try
            {
                await _audit.LogAsync(
                    AuditActions.PitstopEodExported,
                    AuditEntityTypes.PitstopEodBatch,
                    reason: ex.Message,
                    success: false).ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }

            return false;
        }
    }

    private async Task<bool> ArchiveAfterPdfAsync()
    {
        if (!_session.IsManager)
        {
            try
            {
                await _audit.LogAsync(
                    AuditActions.PermissionDenied,
                    AuditEntityTypes.PitstopEodBatch,
                    reason: "Close Pitstop requires Admin/Treasurer.",
                    success: false).ConfigureAwait(true);
            }
            catch
            {
                // audit never blocks
            }

            StatusMessage = "Report saved. A manager needs to close the day.";
            _closeState.SkipArchive = true;
            _closeState.SkipStock = true;
            return true;
        }

        if (_closeState.Archived || _pitstopArchivedAfterCurrentExport)
        {
            _closeState.Archived = true;
            _closeState.SkipArchive = true;
            return true;
        }

        var inputs = BuildInputs();
        var existingBatchId = await _pitstopBatches
            .GetLatestBatchIdForPeriodAsync(inputs.PeriodStartLocal, inputs.PeriodEndLocal)
            .ConfigureAwait(true);
        if (PitstopEodCalculator.IsDuplicateFinalisation(_pitstopArchivedAfterCurrentExport, existingBatchId.HasValue))
        {
            _closeState.Archived = true;
            _closeState.SkipArchive = true;
            _closeState.BatchId ??= existingBatchId;
            StatusMessage = existingBatchId.HasValue
                ? "This Pitstop is already closed. Finishing any remaining stock updates."
                : "This Pitstop is already closed.";
            return true;
        }

        if (!await ConfirmArchivePitstopAsync().ConfigureAwait(true))
        {
            StatusMessage = "Close cancelled. The report is saved — tap Close Pitstop again when you are ready.";
            return false;
        }

        var archived = await ExecuteArchivePitstopAsync().ConfigureAwait(true);
        if (archived)
        {
            _closeState.Archived = true;
        }

        return archived;
    }

    private async Task<bool> ConfirmMismatchExportAsync()
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return false;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Card totals don’t match",
            Content = new TextBlock
            {
                Text =
                    "Square and the POS till show different card amounts. "
                    + "Usually worth checking before you save.\n\n"
                    + "Save the report anyway?",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            },
            PrimaryButtonText = "Save anyway",
            CloseButtonText = "Go back",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        var dialogResult = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        return dialogResult == ContentDialogResult.Primary;
    }

    private async Task<bool> ConfirmArchivePitstopAsync()
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return false;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Close Pitstop and save report?",
            Content = new TextBlock
            {
                Text =
                    "This saves the report, files today’s sales, and updates stock for merch and prizes. "
                    + "You can still open the PDF afterwards.",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            },
            PrimaryButtonText = "Close Pitstop",
            CloseButtonText = "Not yet",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        var dialogResult = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        return dialogResult == ContentDialogResult.Primary;
    }

    private async Task<bool> ExecuteArchivePitstopAsync()
    {
        if (Preview is null)
        {
            return false;
        }

        try
        {
            string? backupBefore = null;
            try
            {
                backupBefore = await _backups.CreateAutomaticBackupAsync("Pitstop archive — before").ConfigureAwait(true);
            }
            catch (Exception)
            {
                backupBefore = null;
            }

            if (string.IsNullOrEmpty(backupBefore))
            {
                try
                {
                    await _audit.LogAsync(
                        AuditActions.BackupFailed,
                        AuditEntityTypes.Backup,
                        reason: "Backup before Pitstop archive failed.",
                        success: false).ConfigureAwait(true);
                }
                catch
                {
                    // ignore audit failures
                }

                var proceed = await ConfirmBackupFailureContinueAsync().ConfigureAwait(true);
                if (!proceed)
                {
                    StatusMessage = "Archive cancelled — backup failed and operator chose to stop.";
                    return false;
                }
            }
            else
            {
                try
                {
                    await _audit.LogAsync(
                        AuditActions.BackupCreated,
                        AuditEntityTypes.Backup,
                        entityId: backupBefore,
                        reason: "Backup before Pitstop archive.").ConfigureAwait(true);
                }
                catch
                {
                    // ignore audit failures
                }
            }

            var expectedCash = Preview?.ExpectedCash;
            var variance = Preview?.CashVariance;
            var notes = string.IsNullOrWhiteSpace(ArchiveNotes) ? null : ArchiveNotes.Trim();

            var inputs = BuildInputs();
            var stockError = await GetStockDeductionErrorAsync().ConfigureAwait(true);
            if (stockError is not null)
            {
                StatusMessage = stockError;
                return false;
            }

            var request = new PitstopEodArchiveRequest
            {
                OperatorName = StaffDisplay is "\u2014" ? null : StaffDisplay,
                OperatorStaffId = _session.ActiveStaffId,
                EventName = EventName,
                PeriodStartLocal = inputs.PeriodStartLocal,
                PeriodEndLocal = inputs.PeriodEndLocal,
                TotalSales = Preview!.GrossSales,
                CashTotal = Preview.PitstopRetailCash,
                CardChargedTotal = Preview.PitstopRetailCard,
                CardBaseProductTotal = Preview.PitstopCardBaseProductTotal,
                CardSurchargeTotal = Preview.PitstopCardSurchargeCollected,
                EstimatedSquareFees = Preview.EstimatedSquareFees,
                NetTotal = Preview.NetEventProfit,
                PdfPath = _lastExportedPdfPath,
                ReportData = Preview,
                ReconciliationWarnings = ReconciliationWarnings.ToList(),
                Notes = notes,
                StartingFloat = InsideFloat,
                CashCounted = CashCounted,
                FloatRemoved = FloatRemoved,
                ExpectedCash = expectedCash,
                CashVariance = variance,
                BackupBeforePath = backupBefore,
            };

            var result = await _pitstopBatches.ArchiveActivePitstopSalesAsync(request).ConfigureAwait(true);
            if (!result.Ok)
            {
                StatusMessage = result.ErrorMessage ?? "Could not archive Pitstop event.";
                try
                {
                    await _audit.LogAsync(
                        AuditActions.PitstopArchived,
                        AuditEntityTypes.PitstopEodBatch,
                        reason: result.ErrorMessage,
                        success: false).ConfigureAwait(true);
                }
                catch
                {
                    // ignore audit failures
                }

                return false;
            }

            _pitstopArchivedAfterCurrentExport = true;
            _closeState.Archived = true;
            _closeState.BatchId = result.BatchId;

            string? backupAfter = null;
            try
            {
                backupAfter = await _backups.CreateAutomaticBackupAsync($"Pitstop archive — after #{result.BatchId}").ConfigureAwait(true);
            }
            catch
            {
                backupAfter = null;
            }

            if (!string.IsNullOrEmpty(backupAfter))
            {
                try
                {
                    await _pitstopBatches.UpdateBackupAfterPathAsync(result.BatchId!.Value, backupAfter).ConfigureAwait(true);
                    await _audit.LogAsync(
                        AuditActions.BackupCreated,
                        AuditEntityTypes.Backup,
                        entityId: backupAfter,
                        reason: $"Backup after Pitstop archive #{result.BatchId}.").ConfigureAwait(true);
                }
                catch
                {
                    // ignore audit failures
                }
            }
            else
            {
                try
                {
                    await _audit.LogAsync(
                        AuditActions.BackupFailed,
                        AuditEntityTypes.Backup,
                        reason: $"Backup after Pitstop archive #{result.BatchId} failed.",
                        success: false).ConfigureAwait(true);
                }
                catch
                {
                    // ignore audit failures
                }
            }

            try
            {
                await _audit.LogAsync(
                    AuditActions.PitstopArchived,
                    AuditEntityTypes.PitstopEodBatch,
                    entityId: result.BatchId?.ToString(CultureInfo.InvariantCulture),
                    amount: request.NetTotal,
                    reason: $"Archived {result.SalesArchived} sale(s), event '{EventName}'.").ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }

            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Archive failed: {ex.Message}. Tap Close to retry — sales have not been double-counted.";
            try
            {
                await _audit.LogAsync(
                    AuditActions.PitstopArchived,
                    AuditEntityTypes.PitstopEodBatch,
                    reason: ex.Message,
                    success: false).ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }

            return false;
        }
    }

    private async Task<bool> ApplyStockAfterArchiveAsync()
    {
        var batchId = _closeState.BatchId;
        if (batchId is null)
        {
            _closeState.SkipStock = true;
            _closeState.StockApplied = true;
            return true;
        }

        var stockDeductionCount = BuildStockDeductionRequests().Count;
        if (stockDeductionCount == 0)
        {
            _closeState.SkipStock = true;
            _closeState.StockApplied = true;
            return true;
        }

        var stockApplied = await ApplyStockDeductionsAsync(batchId.Value).ConfigureAwait(true);
        if (!stockApplied)
        {
            StatusMessage =
                "Pitstop was closed, but stock could not be updated. Tap Close Pitstop again to retry stock only.";
            return false;
        }

        _closeState.StockApplied = true;
        return true;
    }

    private async Task<bool> ConfirmBackupFailureContinueAsync()
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return false;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Backup failed before Pitstop archive",
            Content = new TextBlock
            {
                Text =
                    "The pre-archive database backup could not be created. Continuing without a backup is risky.\n\n"
                    + "Choose Cancel to stop archiving now, or Continue Anyway to archive without a backup.",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            },
            PrimaryButtonText = "Continue Anyway",
            CloseButtonText = "Cancel archive",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        var dialogResult = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        return dialogResult == ContentDialogResult.Primary;
    }

    private async Task ResetPitstopEodFormFieldsAsync()
    {
        IsTestMode = false;
        EventName = "Pitstop";
        _squareReconciliationResult = null;
        _manualCombinedSquareCardGross = null;
        IsSquareManualMode = false;
        InsideFloat = 0m;
        OutsideFloat = 0m;
        SquareFeePercent = 1.75m;
        CashCounted = null;
        OutsideCashCounted = null;
        FloatRemoved = null;
        ArchiveNotes = string.Empty;

        Expenses.Clear();
        CashPrizes.Clear();
        Prizes.Clear();
        await PopulateOutsideLinesFromCatalogAsync(preserveExistingKeys: false).ConfigureAwait(true);
        ReconciliationWarnings.Clear();
        _closeState.Reset();
    }

    private void ResetExportReadyState()
    {
        _lastExportedPdfPath = null;
        _pitstopArchivedAfterCurrentExport = false;
    }

    private void AddExpense()
    {
        Expenses.Add(new EventExpenseEditVm(_input, OnExpenseChanged, EventExpenseKind.Expense));
        OnExpenseChanged();
    }

    private void AddCashPrize()
    {
        CashPrizes.Add(new EventExpenseEditVm(_input, OnExpenseChanged, EventExpenseKind.CashPrize));
        OnExpenseChanged();
    }

    private void OnExpenseChanged()
    {
        OnPropertyChanged(nameof(HasExpenses));
        OnPropertyChanged(nameof(HasCashPrizes));
        OnInputChanged();
    }

    private void RemoveExpense(EventExpenseEditVm? row)
    {
        if (row is not null)
        {
            Expenses.Remove(row);
            OnExpenseChanged();
        }
    }

    private void RemoveCashPrize(EventExpenseEditVm? row)
    {
        if (row is not null)
        {
            CashPrizes.Remove(row);
            OnExpenseChanged();
        }
    }

    private void RemovePrize(MerchPrizeEditVm? row)
    {
        if (row is not null)
        {
            Prizes.Remove(row);
            OnPropertyChanged(nameof(HasStockPrizes));
            OnInputChanged();
        }
    }

    private async Task AddStockPrizeAsync()
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        IReadOnlyList<StockEditorRow> rows;
        try
        {
            rows = await _stock.GetStockRowsAsync(false, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            return;
        }

        var items = rows
            .Where(r => r.IsActive != 0 && !string.IsNullOrWhiteSpace(r.Name))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (items.Count == 0)
        {
            StatusMessage = "No inventory items are available to give as a prize.";
            return;
        }

        var names = items.Select(r => r.Name).ToList();
        var list = new ListView
        {
            ItemsSource = names,
            MaxHeight = 420,
            SelectionMode = ListViewSelectionMode.Single,
        };

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Stock prize",
            Content = list,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        PosContentDialogHelper.ApplyPosStyle(dlg);
        var result = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        if (result != ContentDialogResult.Primary || list.SelectedItem is not string selectedName)
        {
            return;
        }

        var selected = items.First(r => string.Equals(r.Name, selectedName, StringComparison.Ordinal));

        var existing = Prizes.FirstOrDefault(p => p.ItemId == selected.ItemId);
        if (existing is not null)
        {
            existing.Quantity++;
        }
        else
        {
            Prizes.Add(new MerchPrizeEditVm(_input, selected.ItemId, selected.Name, OnPrizeChanged, 1));
        }

        OnPrizeChanged();
    }

    private void OnPrizeChanged()
    {
        OnPropertyChanged(nameof(HasStockPrizes));
        OnInputChanged();
    }

    private async Task LoadItemCostsAsync()
    {
        try
        {
            var rows = await _stock.GetStockRowsAsync(false, CancellationToken.None).ConfigureAwait(true);
            _itemCosts.Clear();
            foreach (var row in rows)
            {
                decimal? cost = row.CostPrice is double d && d > 0d
                    ? decimal.Round((decimal)d, 2, MidpointRounding.AwayFromZero)
                    : null;
                _itemCosts[row.ItemId] = cost;
            }
        }
        catch
        {
            // Unknown costs stay unknown.
        }
    }

    private void ToggleHideZeroOutsideLines() => HideZeroOutsideLines = !HideZeroOutsideLines;

    private async Task LoadTestReportAsync()
    {
        if (!CanRunTestReport)
        {
            StatusMessage = "Test reports require a developer account.";
            return;
        }

        IsBusy = true;
        try
        {
            await PopulateOutsideLinesFromCatalogAsync(preserveExistingKeys: false).ConfigureAwait(true);

            var outsideModels = _outsideLines.Select(x => x.ToModel()).ToList();
            PitstopReportTestDataBuilder.ApplyOutsideLineSamples(outsideModels);
            _outsideLines.Clear();
            foreach (var row in outsideModels)
            {
                _outsideLines.Add(new OutsideLineEditVm(_input, row, OnOutsideLineChanged));
            }

            RebuildOutsideGroups();
            Expenses.Clear();
            CashPrizes.Clear();
            foreach (var expense in PitstopReportTestDataBuilder.BuildSampleExpenses())
            {
                var vm = new EventExpenseEditVm(_input, OnExpenseChanged, expense.Kind)
                {
                    Description = expense.Description,
                    Amount = expense.Amount,
                    PaidFrom = expense.PaidFrom,
                };
                if (expense.Kind == EventExpenseKind.CashPrize)
                {
                    CashPrizes.Add(vm);
                }
                else
                {
                    Expenses.Add(vm);
                }
            }

            Prizes.Clear();
            var merch = _outsideLines.FirstOrDefault(x => x.IsMerch);
            if (merch?.PitstopItemId is > 0)
            {
                Prizes.Add(new MerchPrizeEditVm(_input, merch.PitstopItemId.Value, merch.DisplayLabel, OnPrizeChanged, 2));
            }

            IsTestMode = true;
            EventName = PitstopReportTestDataBuilder.TestEventName;
            ReportDate = DateTimeOffset.Now.Date;
            SquareFeePercent = 1.75m;
            InsideFloat = PitstopReportTestDataBuilder.TestInsideFloat;
            OutsideFloat = PitstopReportTestDataBuilder.TestOutsideFloat;
            CashCounted = PitstopReportTestDataBuilder.TestInsideFloat + PitstopReportTestDataBuilder.TestCashTotal + 15m;
            OutsideCashCounted = PitstopReportTestDataBuilder.TestOutsideFloat
                + _outsideLines.Sum(x => x.LineSales)
                - Expenses
                    .Where(x => x.PaidFrom == EventExpensePaymentSource.OutsideTin)
                    .Sum(x => x.Amount);
            FloatRemoved = PitstopReportTestDataBuilder.TestInsideFloat;
            ArchiveNotes = "TEST REPORT - sample data only. Not a real Pitstop event.";
            HideZeroOutsideLines = false;
            ResetExportReadyState();
            await RefreshSquareAndPreviewAsync().ConfigureAwait(true);
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

    private async Task ClearTestReportAsync()
    {
        if (!IsTestMode)
        {
            return;
        }

        await ResetPitstopEodFormFieldsAsync().ConfigureAwait(true);
        await RefreshPreviewAsync().ConfigureAwait(true);
        StatusMessage = "Test data cleared — showing live Pitstop figures again.";
    }

    private async Task ReloadOutsideLinesFromCatalogAsync()
    {
        IsBusy = true;
        try
        {
            await PopulateOutsideLinesFromCatalogAsync(preserveExistingKeys: false).ConfigureAwait(true);
            StatusMessage = "Merch and raffle lines rebuilt from catalog.";
            ScheduleRefresh();
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

    private async Task PopulateOutsideLinesFromCatalogAsync(bool preserveExistingKeys)
    {
        var seeds = await _outsideCatalog.BuildOutsideSaleTemplateAsync(CancellationToken.None).ConfigureAwait(true);
        var old = _outsideLines.ToDictionary(x => x.Key, x => x.ToModel(), StringComparer.Ordinal);
        _outsideLines.Clear();

        foreach (var t in seeds)
        {
            var row = t;
            if (preserveExistingKeys && old.TryGetValue(t.Key, out var prev))
            {
                row = new OutsideItemSaleRow
                {
                    Key = t.Key,
                    DisplayLabel = t.DisplayLabel,
                    OutsideLineKind = t.OutsideLineKind,
                    PitstopItemId = t.PitstopItemId,
                    SuggestedUnitPrice = t.SuggestedUnitPrice,
                    SoldQty = prev.SoldQty > 0 ? prev.SoldQty : prev.CashQty + prev.CardQty,
                    CashQty = prev.SoldQty > 0 ? prev.SoldQty : prev.CashQty,
                    CashDollars = prev.CashDollars,
                    CardQty = 0,
                    CardDollars = 0m,
                };
            }

            _outsideLines.Add(new OutsideLineEditVm(_input, row, OnOutsideLineChanged));
        }

        RebuildOutsideGroups();
    }

    private void OnOutsideLineChanged()
    {
        RebuildOutsideGroups();
        OnInputChanged();
    }

    private void RebuildOutsideGroups()
    {
        MerchOutsideLines.Clear();
        RaffleOutsideLines.Clear();

        foreach (var line in _outsideLines)
        {
            if (line.IsRaffle)
            {
                RaffleOutsideLines.Add(line);
                continue;
            }

            if (HideZeroOutsideLines && !line.HasAnyValue)
            {
                continue;
            }

            MerchOutsideLines.Add(line);
        }
    }

    private List<(long ItemId, string Label, int Quantity)> BuildStockDeductionRequests()
    {
        var byItem = new Dictionary<long, (string Label, int Quantity)>();

        foreach (var line in _outsideLines.Where(l => l.IsMerch && l.PitstopItemId is > 0))
        {
            var qty = line.SoldQty;
            if (qty <= 0)
            {
                continue;
            }

            var itemId = line.PitstopItemId!.Value;
            if (byItem.TryGetValue(itemId, out var existing))
            {
                byItem[itemId] = (existing.Label, existing.Quantity + qty);
            }
            else
            {
                byItem[itemId] = (line.DisplayLabel, qty);
            }
        }

        foreach (var prize in Prizes.Where(p => p.Quantity > 0))
        {
            if (byItem.TryGetValue(prize.ItemId, out var existing))
            {
                byItem[prize.ItemId] = (existing.Label, existing.Quantity + prize.Quantity);
            }
            else
            {
                byItem[prize.ItemId] = (prize.ItemName, prize.Quantity);
            }
        }

        return byItem
            .Select(kvp => (kvp.Key, kvp.Value.Label, kvp.Value.Quantity))
            .ToList();
    }

    private async Task<string?> GetStockDeductionErrorAsync()
    {
        var deductions = BuildStockDeductionRequests();
        if (deductions.Count == 0)
        {
            return null;
        }

        var rows = await _stock.GetStockRowsAsync(false, CancellationToken.None).ConfigureAwait(true);
        foreach (var (itemId, label, quantity) in deductions)
        {
            var row = rows.FirstOrDefault(r => r.ItemId == itemId);
            if (row is null)
            {
                continue;
            }

            if (row.StockQty - quantity < 0)
            {
                return $"Not enough stock for \"{label}\".";
            }
        }

        return null;
    }

    private async Task<bool> ApplyStockDeductionsAsync(long batchId)
    {
        var deductions = BuildStockDeductionRequests();
        if (deductions.Count == 0)
        {
            return true;
        }

        try
        {
            foreach (var (itemId, _, quantity) in deductions)
            {
                var applied = await _stock
                    .ApplyPitstopEodDeductionAsync(batchId, itemId, quantity, CancellationToken.None)
                    .ConfigureAwait(true);
                if (!applied)
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task BeginEventNameAsync()
    {
        var r = await _input.ShowKeyboardAsync(EventName, "Event name", CancellationToken.None).ConfigureAwait(true);
        if (r is not null)
        {
            EventName = r;
        }
    }

    private async Task BeginReportDateAsync()
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var calendar = new CalendarView
        {
            SelectionMode = CalendarViewSelectionMode.Single,
            MinHeight = 380,
            MaxHeight = 440,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        };
        calendar.SelectedDates.Clear();
        calendar.SelectedDates.Add(ReportDate.DateTime);
        calendar.SetDisplayDate(ReportDate.DateTime);

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Report date",
            Content = calendar,
            PrimaryButtonText = "Use date",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        var result = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        if (result != ContentDialogResult.Primary || calendar.SelectedDates.Count == 0)
        {
            return;
        }

        var selected = calendar.SelectedDates[0].Date;
        ReportDate = new DateTimeOffset(selected, ReportDate.Offset);
    }

    private async Task ShowPosSquareTransactionsAsync()
    {
        if (Preview is null)
        {
            return;
        }

        await ShowSquareTransactionsDialogAsync(
            "POS terminal (Square Terminal 0070)",
            Preview.SquareMatchedPayments).ConfigureAwait(true);
    }

    private async Task ShowOutsideSquareTransactionsAsync()
    {
        if (Preview is null)
        {
            return;
        }

        if (IsSquareManualMode)
        {
            var xamlRoot = _windowHandle.GetXamlRoot();
            if (xamlRoot is null)
            {
                return;
            }

            var dlg = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Outside card (manual)",
                Content = new TextBlock
                {
                    Text = HasManualCombinedSquareCardGross
                        ? $"Outside card ${Money(Preview.OutsideSquareGross)} is derived as combined Square total minus POS terminal card. Outside terminal receipts are not imported in Manual mode."
                        : "Switch stays in Manual — enter the combined Square card gross for the day to derive outside. Outside terminal receipts are not imported.",
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
                },
                CloseButtonText = "Close",
                DefaultButton = ContentDialogButton.Close,
            };
            PosContentDialogHelper.ApplyPosStyle(dlg);
            await dlg.ShowAsync().AsTask().ConfigureAwait(true);
            return;
        }

        await ShowSquareTransactionsDialogAsync(
            "Outside terminal (Flounderers02)",
            Preview.SquareUnmatchedPayments,
            showLineItems: true).ConfigureAwait(true);
    }

    private async Task ShowSquareTransactionsDialogAsync(
        string title,
        IReadOnlyList<SquareReconciliationPaymentRow> rows,
        bool showLineItems = false)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var panel = new StackPanel { Spacing = 10 };
        if (rows.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No transactions in this group.",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            });
        }
        else
        {
            foreach (var row in rows.OrderBy(r => r.PaidAt))
            {
                var card = string.IsNullOrWhiteSpace(row.CardLast4) ? "—" : $"•••• {row.CardLast4}";
                var device = string.IsNullOrWhiteSpace(row.DeviceName) ? "—" : row.DeviceName;
                var receipt = string.IsNullOrWhiteSpace(row.ReceiptNumber) ? "—" : row.ReceiptNumber;
                var header = new TextBlock
                {
                    Text =
                        $"{row.PaidAt.LocalDateTime:hh:mm tt}\n"
                        + $"Receipt {receipt}   Total {row.GrossAmount:0.00}\n"
                        + $"Device: {device}   Card: {card}",
                    TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                };
                panel.Children.Add(header);

                if (showLineItems)
                {
                    if (row.LineItems.Count == 0)
                    {
                        panel.Children.Add(new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(row.OrderLoadWarning)
                                ? "No items loaded for this payment."
                                : row.OrderLoadWarning,
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
                            FontSize = 12,
                            Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["PosTextSecondaryBrush"],
                        });
                    }
                    else
                    {
                        foreach (var line in row.LineItems)
                        {
                            panel.Children.Add(new TextBlock
                            {
                                Text = $"{line.ItemName} x{line.Quantity}   {line.LineTotal:0.00}   ({line.CategoryName})",
                                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
                                FontSize = 13,
                            });
                        }
                    }
                }
                else
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = $"PaymentId: {row.PaymentId}",
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
                        FontSize = 12,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["PosTextSecondaryBrush"],
                    });
                }

                panel.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 4),
                    Background = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["PosBorderBrush"],
                });
            }
        }

        var scroll = new ScrollViewer
        {
            MaxHeight = 520,
            Content = panel,
            VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Auto,
        };

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = scroll,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        await dlg.ShowAsync().AsTask().ConfigureAwait(true);
    }

    private void SetSquareAutoMode()
    {
        if (!IsSquareManualMode && _manualCombinedSquareCardGross is null)
        {
            return;
        }

        IsSquareManualMode = false;
        _manualCombinedSquareCardGross = null;
        OnPropertyChanged(nameof(HasManualCombinedSquareCardGross));
        OnPropertyChanged(nameof(ManualCombinedSquareCardGrossText));
        OnPropertyChanged(nameof(ManualOutsideDerivedText));
        OnPropertyChanged(nameof(ManualCardFallbackStatusText));
        OnPropertyChanged(nameof(SquareReconciliationStatusText));
        _ = RefreshSquareAndPreviewAsync();
    }

    private void SetSquareManualMode()
    {
        if (IsSquareManualMode)
        {
            return;
        }

        IsSquareManualMode = true;
        OnPropertyChanged(nameof(ManualCardFallbackStatusText));
        OnPropertyChanged(nameof(SquareReconciliationStatusText));
        _ = RefreshSquareAndPreviewAsync();
    }

    private async Task BeginManualCombinedSquareAsync()
    {
        if (!IsSquareManualMode)
        {
            SetSquareManualMode();
        }

        var current = _manualCombinedSquareCardGross ?? 0m;
        var r = await _input.ShowNumpadAsync(current, "Combined Square card gross (event day)", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            _manualCombinedSquareCardGross = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
            OnPropertyChanged(nameof(HasManualCombinedSquareCardGross));
            OnPropertyChanged(nameof(ManualCombinedSquareCardGrossText));
            OnPropertyChanged(nameof(ManualOutsideDerivedText));
            OnPropertyChanged(nameof(ManualCardFallbackStatusText));
            OnPropertyChanged(nameof(SquareReconciliationStatusText));
            ScheduleRefresh();
        }
    }

    private void ClearManualCombinedSquare()
    {
        if (_manualCombinedSquareCardGross is null)
        {
            return;
        }

        _manualCombinedSquareCardGross = null;
        OnPropertyChanged(nameof(HasManualCombinedSquareCardGross));
        OnPropertyChanged(nameof(ManualCombinedSquareCardGrossText));
        OnPropertyChanged(nameof(ManualOutsideDerivedText));
        OnPropertyChanged(nameof(ManualCardFallbackStatusText));
        OnPropertyChanged(nameof(SquareReconciliationStatusText));
        ScheduleRefresh();
    }

    private async Task BeginSquareFeeAsync()
    {
        var r = await _input.ShowNumpadAsync(SquareFeePercent, "Square fee %", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            SquareFeePercent = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    private async Task BeginInsideFloatAsync()
    {
        var r = await _input.ShowNumpadAsync(InsideFloat, "Inside float (terminal till)", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            InsideFloat = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    private async Task BeginOutsideFloatAsync()
    {
        var r = await _input.ShowNumpadAsync(OutsideFloat, "Outside float (merch table)", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            OutsideFloat = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    private async Task BeginCashCountedAsync()
    {
        var initial = CashCounted ?? 0m;
        var r = await _input.ShowNumpadAsync(initial, "Cash counted at end of day", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            CashCounted = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    private async Task BeginOutsideCashCountedAsync()
    {
        var initial = OutsideCashCounted ?? 0m;
        var r = await _input.ShowNumpadAsync(initial, "Cash counted in outside merch tin", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            OutsideCashCounted = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    private async Task BeginFloatRemovedAsync()
    {
        var initial = FloatRemoved ?? 0m;
        var r = await _input.ShowNumpadAsync(initial, "Float removed (taken out of till)", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            FloatRemoved = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    private async Task BeginArchiveNotesAsync()
    {
        var r = await _input.ShowKeyboardAsync(ArchiveNotes, "Notes for this Pitstop event", CancellationToken.None).ConfigureAwait(true);
        if (r is not null)
        {
            ArchiveNotes = r;
        }
    }

    private static string Money(decimal v) => v.ToString("0.00", Inv);
}
