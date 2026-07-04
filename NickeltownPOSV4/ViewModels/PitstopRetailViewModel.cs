using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services.Payments;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Views.Panels;

namespace NickeltownPOSV4.ViewModels;

public sealed class PitstopRetailViewModel : ObservableViewModel, IPitstopRetailCartHost
{
    public const string ChipAll = PitstopCatalogChipKeys.All;

    public const string ChipDrinks = PitstopCatalogChipKeys.Drinks;

    public const string ChipFood = PitstopCatalogChipKeys.Food;

    public const string ChipSnacks = "__SNACKS__";

    public const string ChipMerch = PitstopCatalogChipKeys.Merch;

    public const string ChipGeneral = "__GENERAL__";

    private const int ProductPageSize = 8;

    private readonly IPitstopCatalogQuery _catalog;

    private readonly IPitstopRetailSaleRepository _sales;

    private readonly IPitstopHeldSaleRepository _heldSales;

    private readonly IServiceProvider _services;

    private readonly IInputOverlayService _input;

    private readonly ISquareCardPaymentOrchestrator _squarePayments;

    private readonly ISquarePaymentAttemptRepository _paymentAttempts;

    private readonly IUserSessionService _session;

    private readonly ISquareConfigService _squareConfig;

    private readonly PitstopSurchargeConfigLoader _surchargeConfig;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly ISlidePanelService _slidePanel;

    private readonly IRootNavigationCoordinator _rootNav;

    private readonly ISerialCashDrawerService _cashDrawer;

    private readonly IAuthSignOutService _signOut;

    private readonly IAuditLogService _audit;

    private readonly List<PitstopCatalogProductRow> _allProducts = new();

    private readonly List<PitstopCatalogProductRow> _filteredOrdered = new();

    private int _currentProductPage = 1;

    private int _totalProductPages = 1;

    private string _squareStatusPillText = "Square";

    private PitstopCartLineViewModel? _selectedCartLine;

    private readonly PitstopCashNumpadHostViewModel _cashNumpad = new();

    private DispatcherQueueTimer? _clockTimer;

    private bool _catalogReloadRunning;

    private PitstopCategoryChipViewModel? _selectedChip;

    private string _barcodeBuffer = string.Empty;

    private string _statusMessage = string.Empty;

    private string _clockText = string.Empty;

    private bool _isBusy;

    private PitstopPaymentSelection _payment = PitstopPaymentSelection.None;

    private bool _isPaySheetOpen;

    private bool _isCardFeeSheetOpen;

    private bool _isCashSheetOpen;

    private bool _isSendingSquare;

    private string _squareWaitingMessage = string.Empty;

    private CancellationTokenSource? _squareChargeCts;

    private string? _pendingTransactionGuid;

    private bool _paymentInFlight;

    private static readonly JsonSerializerOptions RecoveryJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private decimal _cardSurchargePercent = 1.7m;

    private decimal _pendingCardSubtotal;

    private decimal _pendingCardFee;

    private decimal _pendingCardChargeTotal;

    private decimal _cashSaleTotal;

    private int _heldSaleCount;

    public PitstopRetailViewModel(
        IPitstopCatalogQuery catalog,
        IPitstopRetailSaleRepository sales,
        IPitstopHeldSaleRepository heldSales,
        IServiceProvider services,
        IInputOverlayService input,
        ISquareCardPaymentOrchestrator squarePayments,
        ISquarePaymentAttemptRepository paymentAttempts,
        IUserSessionService session,
        ISquareConfigService squareConfig,
        PitstopSurchargeConfigLoader surchargeConfig,
        ITabWorkspaceRefreshBus refreshBus,
        ISlidePanelService slidePanel,
        IRootNavigationCoordinator rootNav,
        ISerialCashDrawerService cashDrawer,
        IAuditLogService audit,
        IAuthSignOutService signOut)
    {
        _catalog = catalog;
        _sales = sales;
        _heldSales = heldSales;
        _services = services;
        _input = input;
        _squarePayments = squarePayments;
        _paymentAttempts = paymentAttempts;
        _session = session;
        _squareConfig = squareConfig;
        _surchargeConfig = surchargeConfig;
        _refreshBus = refreshBus;
        _slidePanel = slidePanel;
        _rootNav = rootNav;
        _cashDrawer = cashDrawer;
        _audit = audit;
        _signOut = signOut;
        _refreshBus.RefreshRequested += OnSharedPosDataRefreshRequested;

        CategoryChips = new ObservableCollection<PitstopCategoryChipViewModel>();
        ProductTiles = new ObservableCollection<PitstopProductTileViewModel>();
        CartLines = new ObservableCollection<PitstopCartLineViewModel>();

        RefreshCommand = new AsyncRelayCommand(LoadCatalogAsync, () => !IsBusy);
        ClearCartCommand = new RelayCommand(ClearCart, () => !IsBusy && CartLines.Count > 0);
        OpenPaySheetCommand = new RelayCommand(OpenPaySheet, CanStartPayment);
        StartCashCheckoutCommand = new RelayCommand(StartCashCheckout, CanStartPayment);
        StartCardCheckoutCommand = new RelayCommand(StartCardCheckout, CanStartPayment);
        CancelSaleCommand = new RelayCommand(CancelSale, () => !IsBusy && (CartLines.Count > 0 || ShowCheckoutOverlay));
        HoldSaleCommand = new AsyncRelayCommand(HoldSaleAsync, CanHoldSaleExecute);
        CancelCheckoutCommand = new RelayCommand(CancelCheckoutOrCard, () => ShowCheckoutOverlay || IsSendingSquare);
        CancelCardPaymentCommand = new RelayCommand(CancelCardPayment, () => IsSendingSquare);
        ChooseCashPaymentCommand = new RelayCommand(OpenCashSheet, () => !IsBusy && IsPaySheetOpen);
        ChooseCardPaymentCommand = new RelayCommand(OpenCardFeePanel, () => !IsBusy && IsPaySheetOpen);
        CardFeeGoBackCommand = new RelayCommand(GoBackToPaySheet, () => !IsBusy && IsCardFeeSheetOpen);
        CardFeeConfirmCommand = new AsyncRelayCommand(ConfirmCardAndChargeAsync, () => CanConfirmCardFee());
        ConfirmCashSaleCommand = new AsyncRelayCommand(ConfirmCashSaleAsync, () => CanConfirmCash());
        CancelCashSheetCommand = new RelayCommand(CancelCashSheet, () => IsCashSheetOpen && !IsSendingSquare);
        PrevProductPageCommand = new RelayCommand(() => ChangeProductPage(-1), () => !IsBusy && _currentProductPage > 1);
        NextProductPageCommand = new RelayCommand(() => ChangeProductPage(1), () => !IsBusy && _currentProductPage < _totalProductPages);
        RemoveSelectedCartItemCommand = new RelayCommand(RemoveSelectedCartItem, () => !IsBusy && SelectedCartLine is not null);
        SelectChipCommand = new RelayCommand<PitstopCategoryChipViewModel>(SelectChip);
        SignOutCommand = new RelayCommand(SignOut);
    }

    public IRelayCommand SignOutCommand { get; }

    private void SignOut() => _signOut.SignOut();

    public PitstopCashNumpadHostViewModel CashNumpad => _cashNumpad;

    public ObservableCollection<PitstopCategoryChipViewModel> CategoryChips { get; }

    public ObservableCollection<PitstopProductTileViewModel> ProductTiles { get; }

    public ObservableCollection<PitstopCartLineViewModel> CartLines { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand ClearCartCommand { get; }

    public IRelayCommand OpenPaySheetCommand { get; }

    public IRelayCommand StartCashCheckoutCommand { get; }

    public IRelayCommand StartCardCheckoutCommand { get; }

    public IRelayCommand CancelSaleCommand { get; }

    public IAsyncRelayCommand HoldSaleCommand { get; }

    public int HeldSaleCount => _heldSaleCount;

    public bool HasHeldSales => _heldSaleCount > 0;

    public string HoldSaleButtonText =>
        CartLines.Count > 0
            ? "Hold Sale"
            : _heldSaleCount > 0
                ? $"Held sales ({_heldSaleCount})"
                : "Hold Sale";

    public string HoldSaleButtonTooltip =>
        CartLines.Count > 0
            ? "Park this sale and start another customer"
            : _heldSaleCount > 0
                ? "Open held sales to recall or discard"
                : "Hold sale";

    bool IPitstopRetailCartHost.HasActiveCart => CartLines.Count > 0;

    public IRelayCommand CancelCheckoutCommand { get; }

    public IRelayCommand CancelCardPaymentCommand { get; }

    public IRelayCommand ChooseCashPaymentCommand { get; }

    public IRelayCommand ChooseCardPaymentCommand { get; }

    public IRelayCommand CardFeeGoBackCommand { get; }

    public IAsyncRelayCommand CardFeeConfirmCommand { get; }

    public IAsyncRelayCommand ConfirmCashSaleCommand { get; }

    public IRelayCommand CancelCashSheetCommand { get; }

    public IRelayCommand PrevProductPageCommand { get; }

    public IRelayCommand NextProductPageCommand { get; }

    public IRelayCommand RemoveSelectedCartItemCommand { get; }

    public IRelayCommand<PitstopCategoryChipViewModel> SelectChipCommand { get; }

    public bool IsCartEmpty => CartLines.Count == 0;

    public string CartLineCountSummary =>
        CartLines.Count == 0 ? "No items yet" : $"{CartLines.Count} item{(CartLines.Count == 1 ? "" : "s")}";

    public PitstopCategoryChipViewModel? SelectedChip
    {
        get => _selectedChip;
        set
        {
            var prevKey = _selectedChip?.Key;
            if (!SetProperty(ref _selectedChip, value))
            {
                return;
            }

            SyncCategoryChipSelection();

            if (!string.Equals(prevKey, value?.Key, StringComparison.Ordinal))
            {
                _currentProductPage = 1;
            }

            RebuildProductWindow();
        }
    }

    private void SelectChip(PitstopCategoryChipViewModel? chip)
    {
        if (chip is null)
        {
            return;
        }

        SelectedChip = chip;
    }

    private void SyncCategoryChipSelection()
    {
        foreach (var chip in CategoryChips)
        {
            chip.IsSelected = ReferenceEquals(chip, SelectedChip);
        }
    }

    private void NotifyCartPresentation()
    {
        OnPropertyChanged(nameof(IsCartEmpty));
        OnPropertyChanged(nameof(CartLineCountSummary));
        OnPropertyChanged(nameof(CartTotalText));
        OnPropertyChanged(nameof(CartSubtotalLabel));
        OnPropertyChanged(nameof(ReceiptSubtotalText));
        OnPropertyChanged(nameof(ReceiptCardFeeCaption));
        OnPropertyChanged(nameof(ReceiptCardFeeText));
        OnPropertyChanged(nameof(ReceiptCardTotalText));
        OnPropertyChanged(nameof(ReceiptCashTotalText));
        ClearCartCommand.NotifyCanExecuteChanged();
        OpenPaySheetCommand.NotifyCanExecuteChanged();
        StartCashCheckoutCommand.NotifyCanExecuteChanged();
        StartCardCheckoutCommand.NotifyCanExecuteChanged();
        CancelSaleCommand.NotifyCanExecuteChanged();
        HoldSaleCommand.NotifyCanExecuteChanged();
        NotifyHoldSalePresentation();
    }

    private void NotifyHoldSalePresentation()
    {
        OnPropertyChanged(nameof(HeldSaleCount));
        OnPropertyChanged(nameof(HasHeldSales));
        OnPropertyChanged(nameof(HoldSaleButtonText));
        OnPropertyChanged(nameof(HoldSaleButtonTooltip));
    }

    public PitstopCartLineViewModel? SelectedCartLine
    {
        get => _selectedCartLine;
        set
        {
            if (SetProperty(ref _selectedCartLine, value))
            {
                RemoveSelectedCartItemCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int TotalProductPages => _totalProductPages;

    public int CurrentProductPage => _currentProductPage;

    public string ProductPageCaption =>
        $"Page {_currentProductPage} of {_totalProductPages}";

    public string SquareStatusPillText => _squareStatusPillText;

    public string BarcodeBuffer
    {
        get => _barcodeBuffer;
        set => SetProperty(ref _barcodeBuffer, value ?? string.Empty);
    }

    public void AppendBarcodeChar(char ch)
    {
        if (IsBusy || _barcodeBuffer.Length >= 40)
        {
            return;
        }

        _barcodeBuffer += ch;
        OnPropertyChanged(nameof(BarcodeBuffer));
    }

    public void BackspaceBarcode()
    {
        if (_barcodeBuffer.Length == 0)
        {
            return;
        }

        _barcodeBuffer = _barcodeBuffer[..^1];
        OnPropertyChanged(nameof(BarcodeBuffer));
    }

    public void CommitBarcodeScan() => _ = TryCompleteBarcodeAsync();

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ClockText
    {
        get => _clockText;
        private set => SetProperty(ref _clockText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsPaymentLocked));
                NotifyWork();
            }
        }
    }

    public PitstopPaymentSelection Payment
    {
        get => _payment;
        private set => SetProperty(ref _payment, value);
    }

    public bool IsPaySheetOpen
    {
        get => _isPaySheetOpen;
        private set
        {
            if (SetProperty(ref _isPaySheetOpen, value))
            {
                OnCheckoutOverlayChanged();
            }
        }
    }

    public bool IsCardFeeSheetOpen
    {
        get => _isCardFeeSheetOpen;
        private set
        {
            if (SetProperty(ref _isCardFeeSheetOpen, value))
            {
                OnCheckoutOverlayChanged();
            }
        }
    }

    public bool IsCashSheetOpen
    {
        get => _isCashSheetOpen;
        private set
        {
            if (SetProperty(ref _isCashSheetOpen, value))
            {
                OnCheckoutOverlayChanged();
            }
        }
    }

    public bool IsSendingSquare
    {
        get => _isSendingSquare;
        private set
        {
            if (SetProperty(ref _isSendingSquare, value))
            {
                OnPropertyChanged(nameof(ShowSendingSquareOverlay));
                OnPropertyChanged(nameof(IsPaymentLocked));
                NotifyWork();
            }
        }
    }

    public bool ShowSendingSquareOverlay => IsSendingSquare;

    public string SquareWaitingMessage
    {
        get => _squareWaitingMessage;
        private set => SetProperty(ref _squareWaitingMessage, value);
    }

    public bool ShowCheckoutOverlay => IsPaySheetOpen || IsCardFeeSheetOpen || IsCashSheetOpen;

    /// <summary>True while checkout or Square is in progress — blocks catalog edits and duplicate Pay.</summary>
    public bool IsPaymentLocked => IsBusy || IsSendingSquare || _paymentInFlight || ShowCheckoutOverlay;

    public string CashReceivedDisplay => _cashNumpad.AmountDisplay;

    public string CashSaleTotalText => _cashSaleTotal.ToString("0.00", CultureInfo.InvariantCulture);

    public string CashSaleTotalCaption => $"Total ${CashSaleTotalText}";

    public string CashChangeText
    {
        get
        {
            if (!_cashNumpad.TryPeekCurrency(out var received))
            {
                return "0.00";
            }

            return PitstopCashPaymentHelper
                .CalculateChange(received, _cashSaleTotal)
                .ToString("0.00", CultureInfo.InvariantCulture);
        }
    }

    public bool CashConfirmEnabled =>
        _cashNumpad.TryPeekCurrency(out var received)
        && PitstopCashPaymentHelper.IsConfirmEnabled(received, _cashSaleTotal, IsCashSheetOpen);

    public bool CashShortWarning =>
        _cashNumpad.TryPeekCurrency(out var received)
        && PitstopCashPaymentHelper.IsShortWarning(received, _cashSaleTotal, IsCashSheetOpen);

    public string CardFeeSubtotalText => _pendingCardSubtotal.ToString("0.00", CultureInfo.InvariantCulture);

    public string CardFeePercentCaption => $"{_cardSurchargePercent.ToString("0.##", CultureInfo.InvariantCulture)}% Square pass-through";

    public string CardFeeAmountText => _pendingCardFee.ToString("0.00", CultureInfo.InvariantCulture);

    public string CardFeeChargeTotalText => _pendingCardChargeTotal.ToString("0.00", CultureInfo.InvariantCulture);

    public string CardFeeWarning =>
        "Card payments include a Square surcharge. Confirm the customer accepts the new total before sending to Square.";

    public string CartTotalText =>
        PitstopCartHelper.GetCartTotal(CartLines).ToString("0.00", CultureInfo.InvariantCulture);

    public string CartSubtotalLabel
    {
        get
        {
            var itemCount = CartLines.Sum(l => l.Quantity);
            var total = CartTotalText;
            return itemCount <= 1
                ? $"Subtotal ${total}"
                : $"Subtotal ({itemCount} items) ${total}";
        }
    }

    public string OperatorDisplay =>
        string.IsNullOrWhiteSpace(_session.DisplayName) ? "Not signed in" : _session.DisplayName.Trim();

    public string WelcomeText
    {
        get
        {
            var name = _session.DisplayName?.Trim();
            return string.IsNullOrWhiteSpace(name) ? "Welcome" : $"Welcome, {name}";
        }
    }

    public string ReceiptSubtotalText => $"${CartTotalText}";

    public string ReceiptCardFeeCaption =>
        $"Card fee ({_cardSurchargePercent.ToString("0.##", CultureInfo.InvariantCulture)}%)";

    public string ReceiptCardFeeText
    {
        get
        {
            var sub = PitstopCartHelper.GetCartTotal(CartLines);
            var (_, _, fee) = SquareCardFeeCalculator.CalculateCardTotal(sub, _cardSurchargePercent);
            return $"${fee.ToString("0.00", CultureInfo.InvariantCulture)}";
        }
    }

    public string ReceiptCardTotalText
    {
        get
        {
            var sub = PitstopCartHelper.GetCartTotal(CartLines);
            var (_, total, _) = SquareCardFeeCalculator.CalculateCardTotal(sub, _cardSurchargePercent);
            return $"${total.ToString("0.00", CultureInfo.InvariantCulture)}";
        }
    }

    /// <summary>Cash total (subtotal without card surcharge).</summary>
    public string ReceiptCashTotalText => ReceiptSubtotalText;

    public string PitstopModePillText => "Pitstop mode";

    public async Task InitializeAsync()
    {
        StartClock();
        await LoadPreferencesAsync().ConfigureAwait(true);
        await RefreshSquareStatusPillAsync().ConfigureAwait(true);
        await RefreshCatalogFromDatabaseAsync().ConfigureAwait(true);
        await RefreshHeldSaleCountAsync().ConfigureAwait(true);
    }

    public async Task RefreshHeldSaleCountAsync(CancellationToken cancellationToken = default)
    {
        _heldSaleCount = await _heldSales.GetHeldSaleCountAsync(cancellationToken).ConfigureAwait(true);
        NotifyHoldSalePresentation();
        HoldSaleCommand.NotifyCanExecuteChanged();
    }

    public Task RefreshCatalogFromDatabaseAsync() => ReloadCatalogAsync(quiet: true);

    private void OnSharedPosDataRefreshRequested(object? sender, EventArgs e) => ScheduleCatalogRefresh();

    private void ScheduleCatalogRefresh()
    {
        if (DispatcherQueue.GetForCurrentThread() is { } dq)
        {
            dq.TryEnqueue(async () => await ReloadCatalogAsync(quiet: true).ConfigureAwait(true));
            return;
        }

        _ = ReloadCatalogAsync(quiet: true);
    }

    private async Task RefreshSquareStatusPillAsync()
    {
        _squareStatusPillText = await PitstopSquareStatusHelper
            .LoadStatusPillTextAsync(_squareConfig, CancellationToken.None)
            .ConfigureAwait(true);
        OnPropertyChanged(nameof(SquareStatusPillText));
    }

    private void StartClock()
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        _clockTimer ??= dq.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.IsRepeating = true;
        _clockTimer.Tick += (_, _) => BumpClock();
        _clockTimer.Start();
        BumpClock();
    }

    private void BumpClock()
    {
        ClockText = DateTime.Now.ToString("ddd d MMM yyyy  h:mm tt", CultureInfo.CurrentCulture);
    }

    private async Task LoadPreferencesAsync()
    {
        _cardSurchargePercent = await _surchargeConfig
            .LoadCardSurchargePercentAsync(CancellationToken.None)
            .ConfigureAwait(true);
        OnPropertyChanged(nameof(ReceiptCardFeeCaption));
        OnPropertyChanged(nameof(ReceiptCardFeeText));
        OnPropertyChanged(nameof(ReceiptCardTotalText));
        OnPropertyChanged(nameof(ReceiptCashTotalText));
    }

    private void OnCheckoutOverlayChanged()
    {
        OnPropertyChanged(nameof(ShowCheckoutOverlay));
        OnPropertyChanged(nameof(ShowSendingSquareOverlay));
        OpenPaySheetCommand.NotifyCanExecuteChanged();
        StartCashCheckoutCommand.NotifyCanExecuteChanged();
        StartCardCheckoutCommand.NotifyCanExecuteChanged();
        CancelSaleCommand.NotifyCanExecuteChanged();
        HoldSaleCommand.NotifyCanExecuteChanged();
        CancelCheckoutCommand.NotifyCanExecuteChanged();
        ChooseCashPaymentCommand.NotifyCanExecuteChanged();
        ChooseCardPaymentCommand.NotifyCanExecuteChanged();
        CardFeeGoBackCommand.NotifyCanExecuteChanged();
        CardFeeConfirmCommand.NotifyCanExecuteChanged();
        ConfirmCashSaleCommand.NotifyCanExecuteChanged();
        CancelCashSheetCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ReceiptSubtotalText));
        OnPropertyChanged(nameof(ReceiptCardFeeCaption));
        OnPropertyChanged(nameof(ReceiptCardFeeText));
        OnPropertyChanged(nameof(ReceiptCardTotalText));
        OnPropertyChanged(nameof(ReceiptCashTotalText));
        OnPropertyChanged(nameof(CardFeeSubtotalText));
        OnPropertyChanged(nameof(CardFeePercentCaption));
        OnPropertyChanged(nameof(CardFeeAmountText));
        OnPropertyChanged(nameof(CardFeeChargeTotalText));
        OnPropertyChanged(nameof(CashReceivedDisplay));
        OnPropertyChanged(nameof(CashSaleTotalText));
        OnPropertyChanged(nameof(CashSaleTotalCaption));
        OnPropertyChanged(nameof(CashChangeText));
        OnPropertyChanged(nameof(CashConfirmEnabled));
        OnPropertyChanged(nameof(CashShortWarning));
    }

    private void NotifyWork()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        ClearCartCommand.NotifyCanExecuteChanged();
        OpenPaySheetCommand.NotifyCanExecuteChanged();
        StartCashCheckoutCommand.NotifyCanExecuteChanged();
        StartCardCheckoutCommand.NotifyCanExecuteChanged();
        CancelSaleCommand.NotifyCanExecuteChanged();
        HoldSaleCommand.NotifyCanExecuteChanged();
        CancelCheckoutCommand.NotifyCanExecuteChanged();
        CancelCardPaymentCommand.NotifyCanExecuteChanged();
        ChooseCashPaymentCommand.NotifyCanExecuteChanged();
        ChooseCardPaymentCommand.NotifyCanExecuteChanged();
        CardFeeGoBackCommand.NotifyCanExecuteChanged();
        CardFeeConfirmCommand.NotifyCanExecuteChanged();
        ConfirmCashSaleCommand.NotifyCanExecuteChanged();
        CancelCashSheetCommand.NotifyCanExecuteChanged();
        PrevProductPageCommand.NotifyCanExecuteChanged();
        NextProductPageCommand.NotifyCanExecuteChanged();
        RemoveSelectedCartItemCommand.NotifyCanExecuteChanged();
    }

    private bool CanStartPayment() =>
        !IsPaymentLocked && CartLines.Count > 0 && !IsSendingSquare;

    private bool CanConfirmCash() =>
        !IsBusy && !IsSendingSquare && !_paymentInFlight && IsCashSheetOpen;

    private bool CanConfirmCardFee() =>
        !IsBusy && !IsSendingSquare && !_paymentInFlight && IsCardFeeSheetOpen;

    private string BeginPaymentTransaction()
    {
        _pendingTransactionGuid ??= Guid.NewGuid().ToString("N");
        return _pendingTransactionGuid;
    }

    private void ClearPaymentTransaction()
    {
        _pendingTransactionGuid = null;
        _paymentInFlight = false;
    }

    private void StartCashCheckout()
    {
        if (!PaymentTapDebounce.TryEnter() || !CanStartPayment())
        {
            return;
        }

        BeginPaymentTransaction();
        OpenCashSheet();
    }

    private void StartCardCheckout()
    {
        if (!PaymentTapDebounce.TryEnter() || !CanStartPayment())
        {
            return;
        }

        BeginPaymentTransaction();
        OpenCardFeePanel();
    }

    private void CancelSale()
    {
        if (ShowCheckoutOverlay)
        {
            CloseAllCheckoutUi();
        }

        if (CartLines.Count > 0)
        {
            ClearCart();
        }
    }

    private bool CanHoldSaleExecute() =>
        !IsPaymentLocked && (CartLines.Count > 0 || _heldSaleCount > 0);

    private async Task HoldSaleAsync()
    {
        if (CartLines.Count == 0)
        {
            await RefreshHeldSaleCountAsync().ConfigureAwait(true);
            if (_heldSaleCount > 0)
            {
                OpenHeldSalesPanel();
            }

            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var lines = CartLines
                .Select(l => new PitstopHeldSaleLineWrite
                {
                    ItemId = l.ItemId,
                    ItemName = l.DisplayName,
                    Sku = l.Sku,
                    CategoryName = l.CategoryName,
                    SubCategory = l.SubCategory,
                    UnitPrice = l.UnitPrice,
                    Quantity = l.Quantity,
                })
                .ToList();

            await _heldSales
                .SaveHeldSaleAsync(lines, _session.ActiveStaffId, _session.DisplayName, CancellationToken.None)
                .ConfigureAwait(true);

            ClearCart();
            await RefreshHeldSaleCountAsync().ConfigureAwait(true);
            StatusMessage = "Sale held — ready for the next customer.";
        }
        finally
        {
            IsBusy = false;
            NotifyWork();
        }
    }

    private void OpenHeldSalesPanel()
    {
        _slidePanel.Open(_services.GetRequiredService<PitstopHeldSalesPanel>(), 480);
    }

    public async Task<PitstopHeldSaleRecallResult> RecallHeldSaleAsync(
        long heldSaleId,
        CancellationToken cancellationToken = default)
    {
        if (CartLines.Count > 0)
        {
            return PitstopHeldSaleRecallResult.Fail("Hold or clear the current sale before recalling another.");
        }

        if (IsBusy || ShowCheckoutOverlay)
        {
            return PitstopHeldSaleRecallResult.Fail("Finish checkout before recalling a held sale.");
        }

        var detail = await _heldSales.GetHeldSaleAsync(heldSaleId, cancellationToken).ConfigureAwait(true);
        if (detail is null || detail.Lines.Count == 0)
        {
            await RefreshHeldSaleCountAsync(cancellationToken).ConfigureAwait(true);
            return PitstopHeldSaleRecallResult.Fail("That held sale is no longer available.");
        }

        if (!TryBuildRecallPlan(detail.Lines, out var plan, out var planError))
        {
            return PitstopHeldSaleRecallResult.Fail(planError ?? "Could not recall that sale.");
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            foreach (var (row, unitPrice, qty) in plan)
            {
                AddOrMergeLine(row, unitPrice, qty);
            }

            await _heldSales.DeleteHeldSaleAsync(heldSaleId, cancellationToken).ConfigureAwait(true);
            await RefreshHeldSaleCountAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = "Held sale restored to the cart.";
            NotifyCartPresentation();
            return PitstopHeldSaleRecallResult.Success();
        }
        finally
        {
            IsBusy = false;
            NotifyWork();
        }
    }

    private bool TryBuildRecallPlan(
        IReadOnlyList<PitstopHeldSaleLineRow> lines,
        out List<(PitstopCatalogProductRow Row, decimal UnitPrice, int Quantity)> plan,
        out string? errorMessage)
    {
        plan = new List<(PitstopCatalogProductRow, decimal, int)>();
        errorMessage = null;
        var reserved = new Dictionary<long, int>();

        foreach (var line in lines)
        {
            var row = FindProduct(line.ItemId);
            if (row is null)
            {
                errorMessage = $"\"{line.ItemName}\" is no longer on the Pitstop menu.";
                return false;
            }

            reserved.TryGetValue(line.ItemId, out var alreadyReserved);
            if (!PitstopCartHelper.TryValidateAddQuantity(row, alreadyReserved, 0, line.Quantity, out var err))
            {
                errorMessage = err ?? $"Not enough stock for \"{line.ItemName}\".";
                return false;
            }

            reserved[line.ItemId] = alreadyReserved + line.Quantity;
            plan.Add((row, line.UnitPrice, line.Quantity));
        }

        return true;
    }

    private void OpenPaySheet()
    {
        if (!PaymentTapDebounce.TryEnter() || !CanStartPayment())
        {
            return;
        }

        BeginPaymentTransaction();
        Payment = PitstopPaymentSelection.None;
        IsCardFeeSheetOpen = false;
        IsCashSheetOpen = false;
        IsPaySheetOpen = true;
        _ = LogPaymentAuditAsync(
            AuditActions.PaymentStarted,
            _pendingTransactionGuid,
            PitstopCartHelper.GetCartTotal(CartLines),
            "Pitstop Pay sheet opened.");
    }

    private void CancelCheckoutOrCard()
    {
        if (IsSendingSquare)
        {
            CancelCardPayment();
            return;
        }

        CloseAllCheckoutUi();
    }

    private void CancelCardPayment()
    {
        if (!IsSendingSquare)
        {
            return;
        }

        _squareChargeCts?.Cancel();
    }

    private void CloseAllCheckoutUi()
    {
        IsPaySheetOpen = false;
        IsCardFeeSheetOpen = false;
        IsCashSheetOpen = false;
        Payment = PitstopPaymentSelection.None;
        if (!IsSendingSquare && !IsBusy)
        {
            ClearPaymentTransaction();
        }

        OnPropertyChanged(nameof(IsPaymentLocked));
    }

    private void DisposeSquareChargeCts()
    {
        _squareChargeCts?.Dispose();
        _squareChargeCts = null;
    }

    private void OpenCashSheet()
    {
        _cashSaleTotal = PitstopCartHelper.GetCartTotal(CartLines);
        _cashNumpad.Reset(0m);
        _cashNumpad.PropertyChanged -= OnCashNumpadPropertyChanged;
        _cashNumpad.PropertyChanged += OnCashNumpadPropertyChanged;
        IsPaySheetOpen = false;
        IsCashSheetOpen = true;
        RaiseCashUi();
    }

    private void OnCashNumpadPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PitstopCashNumpadHostViewModel.AmountDisplay))
        {
            RaiseCashUi();
        }
    }

    private void RaiseCashUi()
    {
        OnPropertyChanged(nameof(CashReceivedDisplay));
        OnPropertyChanged(nameof(CashChangeText));
        OnPropertyChanged(nameof(CashConfirmEnabled));
        OnPropertyChanged(nameof(CashShortWarning));
        OnPropertyChanged(nameof(CashSaleTotalCaption));
        ConfirmCashSaleCommand.NotifyCanExecuteChanged();
    }

    private void CancelCashSheet()
    {
        _cashNumpad.PropertyChanged -= OnCashNumpadPropertyChanged;
        IsCashSheetOpen = false;
        IsPaySheetOpen = true;
    }

    private void OpenCardFeePanel()
    {
        _pendingCardSubtotal = PitstopCartHelper.GetCartTotal(CartLines);
        (_, _pendingCardChargeTotal, _pendingCardFee) =
            SquareCardFeeCalculator.CalculateCardTotal(_pendingCardSubtotal, _cardSurchargePercent);
        IsPaySheetOpen = false;
        IsCardFeeSheetOpen = true;
        OnCheckoutOverlayChanged();
    }

    private void GoBackToPaySheet()
    {
        IsCardFeeSheetOpen = false;
        IsPaySheetOpen = true;
    }

    private async Task ConfirmCashSaleAsync()
    {
        if (!PaymentTapDebounce.TryEnter() || !CanConfirmCash() || !_cashNumpad.TryPeekCurrency(out var received))
        {
            return;
        }

        received = decimal.Round(received, 2, MidpointRounding.AwayFromZero);
        if (received < _cashSaleTotal)
        {
            return;
        }

        _cashNumpad.PropertyChanged -= OnCashNumpadPropertyChanged;
        IsCashSheetOpen = false;
        Payment = PitstopPaymentSelection.Cash;
        var change = PitstopCashPaymentHelper.CalculateChange(received, _cashSaleTotal);
        await CompleteSaleAsync(
                cashTendered: received,
                cashChange: change)
            .ConfigureAwait(true);
    }

    private async Task ConfirmCardAndChargeAsync()
    {
        if (!PaymentTapDebounce.TryEnter() || !CanConfirmCardFee())
        {
            return;
        }

        IsCardFeeSheetOpen = false;
        Payment = PitstopPaymentSelection.Card;
        await CompleteSaleAsync(cashTendered: null, cashChange: null).ConfigureAwait(true);
    }

    private PitstopCatalogProductRow? FindProduct(long itemId) =>
        _allProducts.FirstOrDefault(p => p.ItemId == itemId);

    private Task LoadCatalogAsync() => ReloadCatalogAsync(quiet: false);

    private async Task ReloadCatalogAsync(bool quiet)
    {
        if (_catalogReloadRunning)
        {
            return;
        }

        if (quiet && (IsSendingSquare || ShowCheckoutOverlay || IsBusy))
        {
            return;
        }

        _catalogReloadRunning = true;
        if (!quiet)
        {
            IsBusy = true;
        }

        StatusMessage = string.Empty;
        try
        {
            var cats = await _catalog.GetPitstopCategoryNamesAsync(CancellationToken.None).ConfigureAwait(true);
            var products = await _catalog.GetPitstopProductsAsync(null, CancellationToken.None).ConfigureAwait(true);
            _allProducts.Clear();
            _allProducts.AddRange(products);

            RebuildCategoryChips(cats);

            if (_allProducts.Count == 0)
            {
                ProductTiles.Clear();
                _filteredOrdered.Clear();
                _totalProductPages = 1;
                _currentProductPage = 1;
                OnPropertyChanged(nameof(TotalProductPages));
                OnPropertyChanged(nameof(CurrentProductPage));
                OnPropertyChanged(nameof(ProductPageCaption));
                PrevProductPageCommand.NotifyCanExecuteChanged();
                NextProductPageCommand.NotifyCanExecuteChanged();
                if (CategoryChips.Count > 0)
                {
                    SelectedChip = CategoryChips[0];
                }

                StatusMessage = "No Pitstop items yet. Mark items \"Show in Pitstop\" in Stock management and set a Pitstop price.";
                return;
            }

            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load catalog: {ex.Message}";
        }
        finally
        {
            if (!quiet)
            {
                IsBusy = false;
            }

            _catalogReloadRunning = false;
        }
    }

    private void RebuildCategoryChips(IReadOnlyList<string> _)
    {
        var preserveKey = SelectedChip?.Key ?? ChipAll;
        _selectedChip = null;
        OnPropertyChanged(nameof(SelectedChip));
        CategoryChips.Clear();
        void AddChip(string key, string label) =>
            CategoryChips.Add(new PitstopCategoryChipViewModel(key, label));

        AddChip(ChipAll, "All Items");
        AddChip(ChipFood, "Food");
        AddChip(ChipDrinks, "Drinks");
        AddChip(ChipMerch, "Merch");

        SelectedChip = CategoryChips.FirstOrDefault(c => string.Equals(c.Key, preserveKey, StringComparison.OrdinalIgnoreCase))
            ?? CategoryChips.FirstOrDefault();
        SyncCategoryChipSelection();
    }

    private void RebuildProductWindow()
    {
        var key = SelectedChip?.Key ?? ChipAll;
        _filteredOrdered.Clear();
        _filteredOrdered.AddRange(PitstopCatalogFilter.FilterAndOrder(_allProducts, key));

        _totalProductPages = PitstopCatalogPager.TotalPages(_filteredOrdered.Count, ProductPageSize);
        _currentProductPage = PitstopCatalogPager.ClampPage(_currentProductPage, _filteredOrdered.Count, ProductPageSize);

        RefreshProductPage();
        OnPropertyChanged(nameof(TotalProductPages));
        OnPropertyChanged(nameof(CurrentProductPage));
        OnPropertyChanged(nameof(ProductPageCaption));
        PrevProductPageCommand.NotifyCanExecuteChanged();
        NextProductPageCommand.NotifyCanExecuteChanged();
    }

    private void RefreshProductPage()
    {
        ProductTiles.Clear();
        foreach (var p in PitstopCatalogPager.GetPage(_filteredOrdered, _currentProductPage, ProductPageSize))
        {
            ProductTiles.Add(
                new PitstopProductTileViewModel(
                    p,
                    id => PitstopCartHelper.QuantityInCart(CartLines, id),
                    OnProductTap));
        }
    }

    private void ChangeProductPage(int delta)
    {
        var next = Math.Clamp(_currentProductPage + delta, 1, _totalProductPages);
        if (next == _currentProductPage)
        {
            return;
        }

        _currentProductPage = next;
        OnPropertyChanged(nameof(CurrentProductPage));
        OnPropertyChanged(nameof(ProductPageCaption));
        RefreshProductPage();
        RefreshAllTiles();
        PrevProductPageCommand.NotifyCanExecuteChanged();
        NextProductPageCommand.NotifyCanExecuteChanged();
    }

    private void RemoveSelectedCartItem()
    {
        if (SelectedCartLine is null)
        {
            return;
        }

        var line = SelectedCartLine;
        SelectedCartLine = null;
        RemoveLine(line);
    }

    private void OnProductTap(PitstopProductTileViewModel tile)
    {
        if (IsBusy || IsPaymentLocked)
        {
            return;
        }

        TapProduct(tile.Source);
    }

    /// <summary>
    /// Adds at the configured Pitstop retail price. <see cref="PitstopCatalogProductRow.UsesOpenPrice"/> applies to bar pours only
    /// (complimentary $0 or open numpad), not Pitstop POS.
    /// </summary>
    private void TapProduct(PitstopCatalogProductRow row)
    {
        if (IsBusy)
        {
            return;
        }

        var unit = decimal.Round((decimal)row.EffectivePitstopPrice, 2, MidpointRounding.AwayFromZero);
        if (unit <= 0m)
        {
            StatusMessage = "No Pitstop price configured for this item.";
            return;
        }

        AddOrMergeLine(row, unit, 1);
    }

    private void AddOrMergeLine(PitstopCatalogProductRow row, decimal unitPrice, int qtyToAdd)
    {
        var existing = CartLines.FirstOrDefault(l => l.ItemId == row.ItemId);
        var otherQty = CartLines.Where(l => l.ItemId == row.ItemId && !ReferenceEquals(l, existing)).Sum(l => l.Quantity);

        if (existing is not null)
        {
            var next = existing.Quantity + qtyToAdd;
            if (!PitstopCartHelper.TryValidateAddQuantity(row, existing.Quantity, otherQty, qtyToAdd, out var err))
            {
                StatusMessage = err!;
                return;
            }

            existing.SyncQuantityFromHost(next);
            StatusMessage = string.Empty;
            NotifyCartPresentation();
            OpenPaySheetCommand.NotifyCanExecuteChanged();
            RefreshAllTiles();
            return;
        }

        if (!PitstopCartHelper.TryValidateAddQuantity(row, 0, otherQty, qtyToAdd, out var errNew))
        {
            StatusMessage = errNew!;
            return;
        }

        var vm = new PitstopCartLineViewModel(
            row.ItemId,
            row.Name,
            row.Sku,
            row.CategoryName,
            row.SubCategoryLabel,
            unitPrice,
            qtyToAdd,
            RemoveLine,
            OnQtyDelta,
            line => SelectedCartLine = line);
        CartLines.Add(vm);
        StatusMessage = string.Empty;
        NotifyCartPresentation();
        ClearCartCommand.NotifyCanExecuteChanged();
        OpenPaySheetCommand.NotifyCanExecuteChanged();
        RefreshAllTiles();
    }

    private void OnQtyDelta(PitstopCartLineViewModel line, int delta)
    {
        var row = FindProduct(line.ItemId);
        var next = line.Quantity + delta;
        if (next < 1)
        {
            RemoveLine(line);
            return;
        }

        var other = CartLines.Where(l => l.ItemId == line.ItemId && !ReferenceEquals(l, line)).Sum(l => l.Quantity);
        if (!PitstopCartHelper.TryValidateDeltaQuantity(row, line.Quantity, other, next, out var err))
        {
            StatusMessage = err!;
            return;
        }

        line.SyncQuantityFromHost(next);
        StatusMessage = string.Empty;
        NotifyCartPresentation();
        OpenPaySheetCommand.NotifyCanExecuteChanged();
        RefreshAllTiles();
    }

    private void RemoveLine(PitstopCartLineViewModel line)
    {
        if (ReferenceEquals(SelectedCartLine, line))
        {
            SelectedCartLine = null;
        }

        CartLines.Remove(line);
        NotifyCartPresentation();
        ClearCartCommand.NotifyCanExecuteChanged();
        OpenPaySheetCommand.NotifyCanExecuteChanged();
        if (CartLines.Count == 0)
        {
            CloseAllCheckoutUi();
        }

        RefreshAllTiles();
    }

    private void ClearCart()
    {
        SelectedCartLine = null;
        CartLines.Clear();
        Payment = PitstopPaymentSelection.None;
        CloseAllCheckoutUi();
        NotifyCartPresentation();
        ClearCartCommand.NotifyCanExecuteChanged();
        OpenPaySheetCommand.NotifyCanExecuteChanged();
        RefreshAllTiles();
    }

    private void RefreshAllTiles()
    {
        foreach (var t in ProductTiles)
        {
            t.RefreshCartBinding();
        }
    }

    public async Task TryCompleteBarcodeAsync()
    {
        var code = (BarcodeBuffer ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return;
        }

        BarcodeBuffer = string.Empty;
        var hit = await _catalog.FindBySkuAsync(code, CancellationToken.None).ConfigureAwait(true);
        if (hit is null)
        {
            StatusMessage = $"No Pitstop item matches barcode/SKU \"{code}\".";
            return;
        }

        TapProduct(hit);
    }

    private async Task CompleteSaleAsync(decimal? cashTendered, decimal? cashChange)
    {
        if (CartLines.Count == 0 || Payment == PitstopPaymentSelection.None || _paymentInFlight)
        {
            return;
        }

        var transactionGuid = BeginPaymentTransaction();
        _paymentInFlight = true;
        OnPropertyChanged(nameof(IsPaymentLocked));
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var lines = PitstopSaleCommitHelper.ToSaleLines(CartLines);
            var baseTotal = PitstopCartHelper.GetCartTotal(CartLines);
            var charged = baseTotal;
            string? squareRef = null;

            string? squareCheckoutId = null;
            string? idempotencyKey = transactionGuid;
            long? paymentAttemptId = null;

            if (Payment == PitstopPaymentSelection.Card)
            {
                charged = PitstopSaleCommitHelper.ResolveCardChargeTotal(baseTotal, _pendingCardChargeTotal);

                SquareWaitingMessage = "Waiting for terminal… Complete the payment on the Square reader.";
                IsSendingSquare = true;
                OnPropertyChanged(nameof(IsPaymentLocked));
                DisposeSquareChargeCts();
                _squareChargeCts = new CancellationTokenSource();
                SquareCardPaymentOutcome cardOutcome;
                try
                {
                    var terminalRequest = PitstopSquareCheckoutBuilder.BuildTerminalRequest(
                        lines,
                        charged,
                        _pendingCardFee);
                    cardOutcome = await _squarePayments.PresentAndLogAsync(
                        new SquareCardPaymentRequest
                        {
                            PaymentType = SquarePaymentAttemptType.PitstopSale,
                            IdempotencyKey = transactionGuid,
                            BaseAmount = baseTotal,
                            SurchargeAmount = _pendingCardFee,
                            ChargedAmount = charged,
                            TerminalRequest = terminalRequest,
                        },
                        _squareChargeCts.Token).ConfigureAwait(true);
                }
                finally
                {
                    IsSendingSquare = false;
                    SquareWaitingMessage = string.Empty;
                    DisposeSquareChargeCts();
                    OnPropertyChanged(nameof(IsPaymentLocked));
                }

                if (!cardOutcome.Approved)
                {
                    StatusMessage = cardOutcome.DeclineReason ?? PitstopSaleCommitHelper.PaymentNotRecordedMessage;
                    Payment = PitstopPaymentSelection.None;
                    CloseAllCheckoutUi();
                    ClearPaymentTransaction();
                    return;
                }

                squareRef = cardOutcome.SquarePaymentId;
                squareCheckoutId = cardOutcome.SquareCheckoutId;
                idempotencyKey = cardOutcome.IdempotencyKey;
                paymentAttemptId = cardOutcome.AttemptId;
            }
            else
            {
                await LogPaymentAuditAsync(
                    AuditActions.PaymentStarted,
                    transactionGuid,
                    charged,
                    "Cash payment confirmation started.").ConfigureAwait(true);
            }

            var pay = PitstopSaleCommitHelper.BuildPaymentCommit(
                Payment,
                baseTotal,
                charged,
                _cardSurchargePercent,
                _pendingCardFee,
                squareRef,
                squareCheckoutId,
                idempotencyKey,
                paymentAttemptId,
                cashTendered,
                cashChange,
                _session.ActiveStaffId,
                _session.DisplayName);

            var r = await _sales.CommitSaleAsync(lines, pay, CancellationToken.None).ConfigureAwait(true);
            if (!r.Ok)
            {
                var failedOnCard = Payment == PitstopPaymentSelection.Card;
                StatusMessage = r.ErrorMessage ?? PitstopSaleCommitHelper.PaymentNotRecordedMessage;
                Payment = PitstopPaymentSelection.None;

                if (failedOnCard && paymentAttemptId is > 0)
                {
                    await RegisterRecoveryAsync(
                            paymentAttemptId.Value,
                            transactionGuid,
                            lines,
                            pay)
                        .ConfigureAwait(true);
                    StatusMessage =
                        $"{StatusMessage} Payment Recovery Required — open Admin → Square Recovery.";
                }

                await _audit.LogAsync(
                    AuditActions.SaleCreated,
                    AuditEntityTypes.PitstopSale,
                    transactionGuid,
                    charged,
                    r.ErrorMessage,
                    success: false).ConfigureAwait(true);

                CloseAllCheckoutUi();
                ClearPaymentTransaction();
                return;
            }

            var savedPayment = Payment;
            await _audit.LogAsync(
                AuditActions.PaymentSaleSaved,
                AuditEntityTypes.PitstopSale,
                r.SaleGuid ?? r.SalePk?.ToString(CultureInfo.InvariantCulture),
                charged,
                $"Pitstop sale ({savedPayment}) saved.").ConfigureAwait(true);
            await _audit.LogAsync(
                AuditActions.SaleCreated,
                AuditEntityTypes.PitstopSale,
                r.SaleGuid ?? r.SalePk?.ToString(CultureInfo.InvariantCulture),
                charged,
                $"Pitstop sale ({savedPayment}) committed.").ConfigureAwait(true);
            await _audit.LogAsync(
                AuditActions.StockDeducted,
                AuditEntityTypes.PitstopSale,
                r.SaleGuid,
                charged,
                "Stock deducted for Pitstop retail sale.").ConfigureAwait(true);
            StatusMessage = PitstopSaleCommitHelper.FormatRecordedSuccessMessage(
                savedPayment,
                charged,
                cashTendered,
                cashChange);

            if (savedPayment == PitstopPaymentSelection.Cash)
            {
                var drawerNote = await TryKickCashDrawerAsync().ConfigureAwait(true);
                if (!string.IsNullOrEmpty(drawerNote))
                {
                    StatusMessage = $"{StatusMessage} {drawerNote}";
                }
            }

            CloseAllCheckoutUi();
            ClearCart();
            ClearPaymentTransaction();
            await LoadCatalogAsync().ConfigureAwait(true);
        }
        finally
        {
            _paymentInFlight = false;
            IsBusy = false;
            OnPropertyChanged(nameof(IsPaymentLocked));
            NotifyWork();
        }
    }

    private async Task RegisterRecoveryAsync(
        long attemptId,
        string transactionGuid,
        IReadOnlyList<PitstopSaleLineCommit> lines,
        PitstopSalePaymentCommit pay)
    {
        var payload = new PitstopPaymentRecoveryPayload
        {
            TransactionGuid = transactionGuid,
            PaymentMethod = pay.PaymentMethod,
            Lines = lines.ToList(),
            Payment = pay,
            StaffId = _session.ActiveStaffId,
            StaffDisplayName = _session.DisplayName,
        };

        var json = JsonSerializer.Serialize(payload, RecoveryJsonOptions);
        await _paymentAttempts.SaveRecoveryPayloadAsync(attemptId, json, CancellationToken.None).ConfigureAwait(true);
        await LogPaymentAuditAsync(
            AuditActions.PaymentRecoveryGenerated,
            transactionGuid,
            pay.ChargedTotal,
            $"Recovery payload saved for Square attempt {attemptId}.").ConfigureAwait(true);
    }

    private Task LogPaymentAuditAsync(
        string action,
        string? entityId,
        decimal amount,
        string reason,
        bool success = true) =>
        _audit.LogAsync(action, AuditEntityTypes.SquareAttempt, entityId, amount, reason, success);

    private async Task<string?> TryKickCashDrawerAsync()
    {
        try
        {
            await _cashDrawer.KickAsync(CancellationToken.None).ConfigureAwait(true);
            return null;
        }
        catch (Exception ex)
        {
            return $"(Cash drawer did not open: {ex.Message})";
        }
    }
}
