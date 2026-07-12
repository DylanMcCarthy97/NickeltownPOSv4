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

namespace NickeltownPOSV4.ViewModels;

public sealed class OutsideLineEditVm : ObservableViewModel
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private readonly IInputOverlayService _input;
    private readonly Action _onValuesChanged;

    private int _cashQty;
    private decimal _cashDollars;
    private int _cardQty;
    private decimal _cardDollars;

    public OutsideLineEditVm(IInputOverlayService input, OutsideItemSaleRow seed, Action onValuesChanged)
    {
        _input = input;
        _onValuesChanged = onValuesChanged;
        Key = seed.Key;
        DisplayLabel = seed.DisplayLabel;
        OutsideLineKind = seed.OutsideLineKind ?? string.Empty;
        PitstopItemId = seed.PitstopItemId;
        SuggestedUnitPrice = seed.SuggestedUnitPrice;
        _cashQty = seed.CashQty;
        _cashDollars = seed.CashDollars;
        _cardQty = seed.CardQty;
        _cardDollars = seed.CardDollars;

        BeginCashQtyCommand = new AsyncRelayCommand(BeginCashQtyAsync);
        BeginCashDollarsCommand = new AsyncRelayCommand(BeginCashDollarsAsync);
        BeginCardQtyCommand = new AsyncRelayCommand(BeginCardQtyAsync);
        BeginCardDollarsCommand = new AsyncRelayCommand(BeginCardDollarsAsync);
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

    public bool HasAnyValue =>
        CashQty > 0 || CashDollars > 0m || CardQty > 0 || CardDollars > 0m;

    public string SuggestedPriceText
    {
        get
        {
            if (IsRaffle)
            {
                return $"${PitstopOutsideLineCatalogBuilder.DefaultRaffleUnitPrice:0.00} per ticket suggested";
            }

            if (SuggestedUnitPrice is decimal p && p > 0m)
            {
                return $"${p:0.00} each";
            }

            return string.Empty;
        }
    }

    public string RowTotalText => Money(CashDollars + CardDollars);

    public int CashQty
    {
        get => _cashQty;
        set
        {
            if (SetProperty(ref _cashQty, value))
            {
                OnPropertyChanged(nameof(CashQtyText));
                OnPropertyChanged(nameof(HasAnyValue));
                OnPropertyChanged(nameof(RowTotalText));
                ApplyCashDollarsFromSuggestedQty();
                _onValuesChanged();
            }
        }
    }

    public string CashQtyText => _cashQty.ToString(Inv);

    public decimal CashDollars
    {
        get => _cashDollars;
        set
        {
            if (SetProperty(ref _cashDollars, value))
            {
                OnPropertyChanged(nameof(CashDollarsText));
                OnPropertyChanged(nameof(HasAnyValue));
                OnPropertyChanged(nameof(RowTotalText));
                _onValuesChanged();
            }
        }
    }

    public string CashDollarsText => Money(_cashDollars);

    public int CardQty
    {
        get => _cardQty;
        set
        {
            if (SetProperty(ref _cardQty, value))
            {
                OnPropertyChanged(nameof(CardQtyText));
                OnPropertyChanged(nameof(HasAnyValue));
                OnPropertyChanged(nameof(RowTotalText));
                ApplyCardDollarsFromSuggestedQty();
                _onValuesChanged();
            }
        }
    }

    public string CardQtyText => _cardQty.ToString(Inv);

    public decimal CardDollars
    {
        get => _cardDollars;
        set
        {
            if (SetProperty(ref _cardDollars, value))
            {
                OnPropertyChanged(nameof(CardDollarsText));
                OnPropertyChanged(nameof(HasAnyValue));
                OnPropertyChanged(nameof(RowTotalText));
                _onValuesChanged();
            }
        }
    }

    public string CardDollarsText => Money(_cardDollars);

    public IAsyncRelayCommand BeginCashQtyCommand { get; }

    public IAsyncRelayCommand BeginCashDollarsCommand { get; }

    public IAsyncRelayCommand BeginCardQtyCommand { get; }

    public IAsyncRelayCommand BeginCardDollarsCommand { get; }

    public OutsideItemSaleRow ToModel() =>
        new()
        {
            Key = Key,
            DisplayLabel = DisplayLabel,
            OutsideLineKind = OutsideLineKind,
            PitstopItemId = PitstopItemId,
            SuggestedUnitPrice = SuggestedUnitPrice,
            CashQty = CashQty,
            CashDollars = CashDollars,
            CardQty = CardQty,
            CardDollars = CardDollars,
        };

    private void ApplyCashDollarsFromSuggestedQty()
    {
        if (SuggestedUnitPrice is not decimal p || p <= 0m)
        {
            return;
        }

        CashDollars = CashQty <= 0
            ? 0m
            : decimal.Round(CashQty * p, 2, MidpointRounding.AwayFromZero);
    }

    private void ApplyCardDollarsFromSuggestedQty()
    {
        if (SuggestedUnitPrice is not decimal p || p <= 0m)
        {
            return;
        }

        CardDollars = CardQty <= 0
            ? 0m
            : decimal.Round(CardQty * p, 2, MidpointRounding.AwayFromZero);
    }

    private async Task BeginCashQtyAsync()
    {
        var r = await _input.ShowIntegerNumpadAsync(CashQty, $"{DisplayLabel} — cash qty", 0, 9999999, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            CashQty = r.Value;
        }
    }

    private async Task BeginCashDollarsAsync()
    {
        var r = await _input.ShowNumpadAsync(CashDollars, $"{DisplayLabel} — cash $", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            CashDollars = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
    }

    private async Task BeginCardQtyAsync()
    {
        var r = await _input.ShowIntegerNumpadAsync(CardQty, $"{DisplayLabel} — card qty", 0, 9999999, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            CardQty = r.Value;
        }
    }

    private async Task BeginCardDollarsAsync()
    {
        var r = await _input.ShowNumpadAsync(CardDollars, $"{DisplayLabel} — card $", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            CardDollars = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
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

    public EventExpenseEditVm(IInputOverlayService input, Action onValuesChanged)
    {
        _input = input;
        _onValuesChanged = onValuesChanged;
        BeginDescriptionCommand = new AsyncRelayCommand(BeginDescriptionAsync);
        BeginAmountCommand = new AsyncRelayCommand(BeginAmountAsync);
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

    public IAsyncRelayCommand BeginDescriptionCommand { get; }

    public IAsyncRelayCommand BeginAmountCommand { get; }

    private async Task BeginDescriptionAsync()
    {
        var r = await _input.ShowKeyboardAsync(Description, "Expense description", CancellationToken.None).ConfigureAwait(true);
        if (r is not null)
        {
            Description = r;
        }
    }

    private async Task BeginAmountAsync()
    {
        var r = await _input.ShowNumpadAsync(Amount, "Expense amount", false, CancellationToken.None).ConfigureAwait(true);
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

    private readonly ObservableCollection<OutsideLineEditVm> _outsideLines = new();
    private CancellationTokenSource? _refreshDebounceCts;

    private string _eventName = "Pitstop";
    private DateTimeOffset _reportDate = DateTimeOffset.Now.Date;
    private decimal _combinedSquare;
    private decimal _squareFeePercent = 1.75m;
    private decimal _insideFloat;
    private decimal _outsideFloat;
    private decimal? _cashCounted;
    private decimal? _floatRemoved;
    private string _archiveNotes = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isRefreshing;
    private bool _hideZeroOutsideLines = true;
    private bool _mismatchExportAcknowledged;
    private string? _lastExportedPdfPath;
    private bool _pitstopArchivedAfterCurrentExport;
    private bool _isTestMode;
    private PitstopReportData? _preview;

    private PitstopEodReconciliationReport? _pitstopReconciliationReport;

    public PitstopEndOfDayReportViewModel(
        PitstopReportService report,
        PitstopEodReconciliationService pitstopReconciliation,
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
        IAuditLogService audit)
    {
        _report = report;
        _pitstopReconciliation = pitstopReconciliation;
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

        MerchOutsideLines = new ObservableCollection<OutsideLineEditVm>();
        RaffleOutsideLines = new ObservableCollection<OutsideLineEditVm>();
        Expenses = new ObservableCollection<EventExpenseEditVm>();
        Prizes = new ObservableCollection<MerchPrizeEditVm>();

        AddExpenseCommand = new RelayCommand(AddExpense);
        RemoveExpenseCommand = new RelayCommand<EventExpenseEditVm>(RemoveExpense);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync, () => !IsBusy && Preview is not null);
        ReloadOutsideLinesFromCatalogCommand = new AsyncRelayCommand(ReloadOutsideLinesFromCatalogAsync, () => !IsBusy);
        ToggleHideZeroOutsideLinesCommand = new RelayCommand(ToggleHideZeroOutsideLines);
        LoadTestReportCommand = new AsyncRelayCommand(LoadTestReportAsync, () => !IsBusy && CanRunTestReport);
        ClearTestReportCommand = new AsyncRelayCommand(ClearTestReportAsync, () => IsTestMode);

        BeginEventNameCommand = new AsyncRelayCommand(BeginEventNameAsync);
        BeginCombinedSquareCommand = new AsyncRelayCommand(BeginCombinedSquareAsync);
        BeginSquareFeeCommand = new AsyncRelayCommand(BeginSquareFeeAsync);
        BeginInsideFloatCommand = new AsyncRelayCommand(BeginInsideFloatAsync);
        BeginOutsideFloatCommand = new AsyncRelayCommand(BeginOutsideFloatAsync);
        BeginCashCountedCommand = new AsyncRelayCommand(BeginCashCountedAsync);
        BeginFloatRemovedCommand = new AsyncRelayCommand(BeginFloatRemovedAsync);
        BeginArchiveNotesCommand = new AsyncRelayCommand(BeginArchiveNotesAsync);
        ClearCashCountedCommand = new RelayCommand(() => CashCounted = null);
        ClearFloatRemovedCommand = new RelayCommand(() => FloatRemoved = null);
    }

    public ObservableCollection<OutsideLineEditVm> MerchOutsideLines { get; }

    public ObservableCollection<OutsideLineEditVm> RaffleOutsideLines { get; }

    public ObservableCollection<EventExpenseEditVm> Expenses { get; }

    public ObservableCollection<MerchPrizeEditVm> Prizes { get; }

    public IRelayCommand AddExpenseCommand { get; }

    public IRelayCommand<EventExpenseEditVm> RemoveExpenseCommand { get; }

    public IAsyncRelayCommand ExportPdfCommand { get; }

    public IAsyncRelayCommand ReloadOutsideLinesFromCatalogCommand { get; }

    public IRelayCommand ToggleHideZeroOutsideLinesCommand { get; }

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

    public IAsyncRelayCommand BeginCombinedSquareCommand { get; }

    public IAsyncRelayCommand BeginSquareFeeCommand { get; }

    public IAsyncRelayCommand BeginInsideFloatCommand { get; }

    public IAsyncRelayCommand BeginOutsideFloatCommand { get; }

    public IAsyncRelayCommand BeginCashCountedCommand { get; }

    public IAsyncRelayCommand BeginFloatRemovedCommand { get; }

    public IAsyncRelayCommand BeginArchiveNotesCommand { get; }

    public IRelayCommand ClearCashCountedCommand { get; }

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
                ResetExportReadyState();
                ScheduleRefresh();
            }
        }
    }

    public string StaffDisplay =>
        string.IsNullOrWhiteSpace(_session.DisplayName) ? "\u2014" : _session.DisplayName.Trim();

    public string ReportPeriodCaption
    {
        get
        {
            var day = ReportDate.Date;
            return $"{day:dddd d MMMM yyyy}";
        }
    }

    public decimal CombinedSquareCardGross
    {
        get => _combinedSquare;
        set
        {
            if (SetProperty(ref _combinedSquare, value))
            {
                OnPropertyChanged(nameof(CombinedSquareCardGrossText));
                _mismatchExportAcknowledged = false;
                ScheduleRefresh();
            }
        }
    }

    public string CombinedSquareCardGrossText => Money(CombinedSquareCardGross);

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
                OnPropertyChanged(nameof(ExpectedCashText));
                OnPropertyChanged(nameof(CashVarianceText));
                OnPropertyChanged(nameof(HasCashVariance));
                ScheduleRefresh();
            }
        }
    }

    public string CashCountedText => CashCounted is null ? "—" : Money(CashCounted.Value);

    public decimal? FloatRemoved
    {
        get => _floatRemoved;
        set
        {
            if (SetProperty(ref _floatRemoved, value))
            {
                OnPropertyChanged(nameof(FloatRemovedText));
            }
        }
    }

    public string FloatRemovedText => FloatRemoved is null ? "—" : Money(FloatRemoved.Value);

    public decimal? ExpectedCash => Preview is null ? null : InsideFloat + Preview.PitstopRetailCash;

    public string ExpectedCashText => ExpectedCash is null ? "—" : Money(ExpectedCash.Value);

    public decimal? CashVariance =>
        CashCounted is decimal counted && ExpectedCash is decimal expected ? counted - expected : null;

    public string CashVarianceText => CashVariance is null ? "—" : Money(CashVariance.Value);

    public bool HasCashVariance => CashVariance is decimal v && v != 0m;

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
        HideZeroOutsideLines ? "Show all merch lines" : "Hide unused merch lines";

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
                ReloadOutsideLinesFromCatalogCommand.NotifyCanExecuteChanged();
                LoadTestReportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public PitstopReportData? Preview
    {
        get => _preview;
        private set
        {
            if (SetProperty(ref _preview, value))
            {
                ExportPdfCommand.NotifyCanExecuteChanged();
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
                OnPropertyChanged(nameof(SquareBatchText));
                OnPropertyChanged(nameof(SquareTerminalDiffText));
                OnPropertyChanged(nameof(LastRefreshedText));
                OnPropertyChanged(nameof(ExpectedCash));
                OnPropertyChanged(nameof(ExpectedCashText));
                OnPropertyChanged(nameof(CashVariance));
                OnPropertyChanged(nameof(CashVarianceText));
                OnPropertyChanged(nameof(HasCashVariance));
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
                $"Square batch ${Money(Preview.CombinedSquareCardGross)} does not match Pitstop terminal card ${Money(Preview.PitstopRetailCard)} "
                + $"(diff ${Money(Preview.OutsideCardDifference)}). Check your Square close-out vs POS.";
        }
    }

    public string CashToDepositText => Preview is null ? "\u2014" : Money(Preview.CashToDeposit);

    public string NetProfitText => Preview is null ? "\u2014" : Money(Preview.NetEventProfit);

    public string GrossSalesText => Preview is null ? "\u2014" : Money(Preview.GrossSales);

    public string PitstopCashText => Preview is null ? "\u2014" : Money(Preview.PitstopRetailCash);

    public string PitstopCardText => Preview is null ? "\u2014" : Money(Preview.PitstopRetailCard);

    public string OutsideCashText => Preview is null ? "\u2014" : Money(Preview.OutsideCashTotal);

    public string OutsideCardText => Preview is null ? "\u2014" : Money(Preview.OutsideMerchRaffleCardTotal);

    public string SquareBatchText => Preview is null ? "\u2014" : Money(Preview.CombinedSquareCardGross);

    public string SquareTerminalDiffText => Preview is null ? "\u2014" : Money(Preview.OutsideCardDifference);

    public string LastRefreshedText =>
        Preview is null ? "Not calculated yet" : $"Updated {DateTime.Now:HH:mm:ss}";

    public string ReconciliationSummaryText =>
        _pitstopReconciliationReport is null
            ? string.Empty
            : $"Pitstop reconciliation — cash ${_pitstopReconciliationReport.CashSales:0.00}, "
              + $"card ${_pitstopReconciliationReport.CardSalesCharged:0.00}, "
              + $"surcharge ${_pitstopReconciliationReport.CardSurchargeCollected:0.00}, "
              + $"est. Square fees ${_pitstopReconciliationReport.EstimatedSquareFee:0.00}, "
              + $"recorded ${_pitstopReconciliationReport.TotalRecordedSales:0.00}.";

    public ObservableCollection<string> ReconciliationWarnings { get; } = new();

    public async Task InitializeAsync()
    {
        var hasStaleOutsideLines = _outsideLines.Any(l => !l.IsMerch && !l.IsRaffle);
        if (_outsideLines.Count == 0 || hasStaleOutsideLines)
        {
            await PopulateOutsideLinesFromCatalogAsync(preserveExistingKeys: false).ConfigureAwait(true);
        }
        else
        {
            RebuildOutsideGroups();
        }

        SyncPrizeRowsFromMerch(preserveQuantities: true);
        await RefreshPreviewAsync().ConfigureAwait(true);
    }

    private void OnInputChanged()
    {
        _mismatchExportAcknowledged = false;
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

            if (CashCounted is decimal counted && Preview is not null)
            {
                var expected = InsideFloat + Preview.PitstopRetailCash;
                var variance = counted - expected;
                if (Math.Abs(variance) >= 0.01m)
                {
                    var sign = variance > 0 ? "over" : "short";
                    ReconciliationWarnings.Add(
                        $"Cash variance {sign} by {Math.Abs(variance):C2} (counted {counted:C2} vs expected {expected:C2}).");
                }
            }

            OnPropertyChanged(nameof(ReconciliationSummaryText));
            StatusMessage = data.OutsideCardMismatch
                ? "Square batch does not match Pitstop terminal card — review before export."
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
            CombinedSquareCardGross = CombinedSquareCardGross,
            SquareFeePercent = SquareFeePercent,
            InsideFloat = InsideFloat,
            OutsideFloat = OutsideFloat,
            CashCounted = CashCounted,
            FloatRemoved = FloatRemoved,
            UseTestPosData = IsTestMode,
        };

        foreach (var w in ReconciliationWarnings)
        {
            if (!string.IsNullOrWhiteSpace(w))
            {
                inputs.Warnings.Add(w);
            }
        }

        foreach (var o in _outsideLines)
        {
            inputs.OutsideLines.Add(o.ToModel());
        }

        foreach (var e in Expenses)
        {
            inputs.Expenses.Add(new EventExpenseRow { Description = e.Description, Amount = e.Amount });
        }

        foreach (var p in Prizes.Where(x => x.Quantity > 0))
        {
            inputs.PrizeGiveaways.Add(new MerchPrizeGiveawayRow { ItemId = p.ItemId, ItemName = p.ItemName, Quantity = p.Quantity });
        }

        return inputs;
    }

    private async Task ExportPdfAsync()
    {
        if (Preview is null)
        {
            return;
        }

        if (Preview.OutsideCardMismatch && !_mismatchExportAcknowledged)
        {
            _mismatchExportAcknowledged = true;
            StatusMessage = "Square mismatch — tap Export PDF again to save anyway.";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        var exportSucceeded = false;
        try
        {
            var bytes = PitstopReportPdfExporter.Build(Preview);
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
            StatusMessage = _launcher.TryLaunch(path)
                ? $"Saved and opened: {path}"
                : $"Saved: {path}";
            _mismatchExportAcknowledged = false;
            _lastExportedPdfPath = path;
            _pitstopArchivedAfterCurrentExport = false;
            exportSucceeded = true;

            try
            {
                await _audit.LogAsync(
                    AuditActions.PitstopEodExported,
                    AuditEntityTypes.PitstopEodBatch,
                    entityId: path,
                    amount: Preview.GrossSales,
                    reason: $"EOD PDF exported for {ReportPeriodCaption}.").ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
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
        }
        finally
        {
            IsBusy = false;
        }

        if (exportSucceeded)
        {
            await OfferArchivePitstopAfterExportAsync().ConfigureAwait(true);
        }
    }

    private async Task OfferArchivePitstopAfterExportAsync()
    {
        if (IsTestMode)
        {
            StatusMessage = "Test report saved. Archive is disabled for sample data.";
            return;
        }

        if (!_session.IsManager)
        {
            try
            {
                await _audit.LogAsync(
                    AuditActions.PermissionDenied,
                    AuditEntityTypes.PitstopEodBatch,
                    reason: "Archive Pitstop requires Admin/Treasurer.",
                    success: false).ConfigureAwait(true);
            }
            catch
            {
                // audit never blocks
            }

            return;
        }

        if (_pitstopArchivedAfterCurrentExport)
        {
            return;
        }

        var inputs = BuildInputs();
        var activeCount = await _pitstopBatches
            .GetActivePitstopSaleCountForPeriodAsync(inputs.PeriodStartLocal, inputs.PeriodEndLocal)
            .ConfigureAwait(true);
        if (activeCount == 0)
        {
            StatusMessage = "Report saved. Pitstop sales for this period were already archived.";
            return;
        }

        if (!await ConfirmArchivePitstopAsync().ConfigureAwait(true))
        {
            return;
        }

        await ExecuteArchivePitstopAsync().ConfigureAwait(true);
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
            Title = "Archive this Pitstop now?",
            Content = new TextBlock
            {
                Text =
                    "This will move the sales to Previous Pitstops and stop them counting in the next EOD. "
                    + "Outside merch and prize giveaway quantities will be deducted from main stock. "
                    + "Archived terminal sales stock will not be restored.",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            },
            PrimaryButtonText = "Archive Pitstop",
            CloseButtonText = "Not Now",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        var dialogResult = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        return dialogResult == ContentDialogResult.Primary;
    }

    private async Task ExecuteArchivePitstopAsync()
    {
        if (Preview is null)
        {
            return;
        }

        IsBusy = true;
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
                    return;
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

            var expectedCash = Preview is null ? (decimal?)null : InsideFloat + Preview.PitstopRetailCash;
            var variance = expectedCash is decimal exp && CashCounted is decimal cnt ? (decimal?)(cnt - exp) : null;
            var notes = string.IsNullOrWhiteSpace(ArchiveNotes) ? null : ArchiveNotes.Trim();

            var inputs = BuildInputs();
            var stockError = await GetStockDeductionErrorAsync().ConfigureAwait(true);
            if (stockError is not null)
            {
                StatusMessage = stockError;
                return;
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

                return;
            }

            _pitstopArchivedAfterCurrentExport = true;

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

            var stockDeductionCount = BuildStockDeductionRequests().Count;
            if (stockDeductionCount > 0)
            {
                var stockApplied = await ApplyStockDeductionsAsync().ConfigureAwait(true);
                if (!stockApplied)
                {
                    StatusMessage =
                        $"Archived {result.SalesArchived} Pitstop sale(s), but stock could not be updated. Check stock levels.";
                    return;
                }
            }

            await ResetPitstopEodFormFieldsAsync().ConfigureAwait(true);
            ResetExportReadyState();
            _mismatchExportAcknowledged = false;
            await RefreshPreviewAsync().ConfigureAwait(true);
            StatusMessage = stockDeductionCount > 0
                ? $"Saved to Previous Pitstops and exported. {result.SalesArchived} sale(s) archived, stock updated. Form reset for the next event."
                : $"Saved to Previous Pitstops and exported. {result.SalesArchived} sale(s) archived. Form reset for the next event.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Archive failed: {ex.Message}";
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
        }
        finally
        {
            IsBusy = false;
        }
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
        CombinedSquareCardGross = 0m;
        InsideFloat = 0m;
        OutsideFloat = 0m;
        SquareFeePercent = 1.75m;
        CashCounted = null;
        FloatRemoved = null;
        ArchiveNotes = string.Empty;

        Expenses.Clear();
        await PopulateOutsideLinesFromCatalogAsync(preserveExistingKeys: false).ConfigureAwait(true);
        SyncPrizeRowsFromMerch(preserveQuantities: false);
        ReconciliationWarnings.Clear();
    }

    private void ResetExportReadyState()
    {
        _lastExportedPdfPath = null;
        _pitstopArchivedAfterCurrentExport = false;
    }

    private void AddExpense() => Expenses.Add(new EventExpenseEditVm(_input, OnInputChanged));

    private void RemoveExpense(EventExpenseEditVm? row)
    {
        if (row is not null)
        {
            Expenses.Remove(row);
            OnInputChanged();
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
            SyncPrizeRowsFromMerch(preserveQuantities: false);
            if (Prizes.Count > 0)
            {
                Prizes[0].Quantity = 2;
            }

            Expenses.Clear();
            foreach (var expense in PitstopReportTestDataBuilder.BuildSampleExpenses())
            {
                var vm = new EventExpenseEditVm(_input, OnInputChanged)
                {
                    Description = expense.Description,
                    Amount = expense.Amount,
                };
                Expenses.Add(vm);
            }

            IsTestMode = true;
            EventName = PitstopReportTestDataBuilder.TestEventName;
            ReportDate = DateTimeOffset.Now.Date;
            CombinedSquareCardGross = PitstopReportTestDataBuilder.TestCardChargedTotal;
            SquareFeePercent = 1.75m;
            InsideFloat = PitstopReportTestDataBuilder.TestInsideFloat;
            OutsideFloat = PitstopReportTestDataBuilder.TestOutsideFloat;
            CashCounted = PitstopReportTestDataBuilder.TestInsideFloat + PitstopReportTestDataBuilder.TestCashTotal + 15m;
            FloatRemoved = PitstopReportTestDataBuilder.TestInsideFloat;
            ArchiveNotes = "TEST REPORT - sample data only. Not a real Pitstop event.";
            HideZeroOutsideLines = false;
            ResetExportReadyState();
            _mismatchExportAcknowledged = false;
            await RefreshPreviewAsync().ConfigureAwait(true);
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
            SyncPrizeRowsFromMerch(preserveQuantities: false);
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
                    CashQty = prev.CashQty,
                    CashDollars = prev.CashDollars,
                    CardQty = prev.CardQty,
                    CardDollars = prev.CardDollars,
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

    private void SyncPrizeRowsFromMerch(bool preserveQuantities)
    {
        var oldQty = Prizes.ToDictionary(p => p.ItemName, p => p.Quantity, StringComparer.OrdinalIgnoreCase);
        Prizes.Clear();
        var models = _outsideLines.Select(x => x.ToModel()).ToList();
        foreach (var (id, name) in PitstopOutsideLineCatalogBuilder.BuildMerchPrizeSeeds(models))
        {
            var q = 0;
            if (preserveQuantities && oldQty.TryGetValue(name, out var pq))
            {
                q = pq;
            }

            Prizes.Add(new MerchPrizeEditVm(_input, id, name, OnInputChanged, q));
        }
    }

    private List<(long ItemId, string Label, int Quantity)> BuildStockDeductionRequests()
    {
        var byItem = new Dictionary<long, (string Label, int Quantity)>();

        foreach (var line in _outsideLines.Where(l => l.IsMerch && l.PitstopItemId is long itemId && itemId > 0))
        {
            var qty = line.CashQty + line.CardQty;
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

    private async Task<bool> ApplyStockDeductionsAsync()
    {
        var deductions = BuildStockDeductionRequests();
        if (deductions.Count == 0)
        {
            return true;
        }

        try
        {
            var rows = await _stock.GetStockRowsAsync(false, CancellationToken.None).ConfigureAwait(true);
            foreach (var (itemId, _, quantity) in deductions)
            {
                var row = rows.FirstOrDefault(r => r.ItemId == itemId);
                if (row is null)
                {
                    continue;
                }

                var next = row.StockQty - quantity;
                if (next < 0)
                {
                    return false;
                }

                await _stock
                    .UpdateStockRowAsync(itemId, next, row.TrackStock, row.CategoryId, CancellationToken.None)
                    .ConfigureAwait(true);
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

    private async Task BeginCombinedSquareAsync()
    {
        var r = await _input.ShowNumpadAsync(CombinedSquareCardGross, "Square batch card gross", false, CancellationToken.None).ConfigureAwait(true);
        if (r.HasValue)
        {
            CombinedSquareCardGross = decimal.Round(r.Value, 2, MidpointRounding.AwayFromZero);
        }
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
