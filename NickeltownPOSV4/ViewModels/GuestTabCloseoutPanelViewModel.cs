using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Payments;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels;

public sealed class GuestTabCloseoutPanelViewModel : ObservableViewModel
{
    private enum CloseoutStep
    {
        Summary,
        Cash,
        CardConfirm,
    }

    private readonly ITabFundsService _funds;
    private readonly ITabManagementRepository _tabs;
    private readonly IAddFundsSession _session;
    private readonly ITabWorkspaceRefreshBus _refreshBus;
    private readonly ISlidePanelService _slide;
    private readonly IInputOverlayService _inputOverlay;
    private readonly ITabWorkspaceUndoStack _undo;
    private readonly ISquareCardPaymentOrchestrator _squarePayments;
    private readonly ISquareConfigService _squareConfig;
    private readonly PitstopCashNumpadHostViewModel _cashNumpad = new();

    private string _statusMessage = string.Empty;
    private bool _isBusy;
    private bool _isSendingSquare;
    private string _squareWaitingMessage = string.Empty;
    private CancellationTokenSource? _squareChargeCts;
    private CloseoutStep _step = CloseoutStep.Summary;
    private decimal _amountDue;
    private decimal _cardSurchargePercent = 1.7m;
    private decimal _cardFee;
    private decimal _cardChargeTotal;

    public GuestTabCloseoutPanelViewModel(
        ITabFundsService funds,
        ITabManagementRepository tabs,
        IAddFundsSession session,
        ITabWorkspaceRefreshBus refreshBus,
        ISlidePanelService slide,
        IInputOverlayService inputOverlay,
        ITabWorkspaceUndoStack undo,
        ISquareCardPaymentOrchestrator squarePayments,
        ISquareConfigService squareConfig)
    {
        _funds = funds;
        _tabs = tabs;
        _session = session;
        _refreshBus = refreshBus;
        _slide = slide;
        _inputOverlay = inputOverlay;
        _undo = undo;
        _squarePayments = squarePayments;
        _squareConfig = squareConfig;

        CancelCommand = new RelayCommand(CancelOrClosePanel);
        CancelSquarePaymentCommand = new RelayCommand(CancelSquarePayment, () => IsSendingSquare);
        BackCommand = new RelayCommand(GoBack, () => _step != CloseoutStep.Summary && !IsPaymentProcessing);
        PayCashCommand = new AsyncRelayCommand(StartCashAsync, CanPay);
        PayCardCommand = new RelayCommand(OpenCardConfirm, CanPayCard);
        ConfirmCashCommand = new AsyncRelayCommand(ConfirmCashAsync, () => !IsPaymentProcessing && _step == CloseoutStep.Cash && CashConfirmEnabled);
        ConfirmCardCommand = new AsyncRelayCommand(ConfirmCardAsync, () => !IsPaymentProcessing && _step == CloseoutStep.CardConfirm);
    }

    public PitstopCashNumpadHostViewModel CashNumpad => _cashNumpad;

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand CancelSquarePaymentCommand { get; }

    public IRelayCommand BackCommand { get; }

    public IAsyncRelayCommand PayCashCommand { get; }

    public IRelayCommand PayCardCommand { get; }

    public IAsyncRelayCommand ConfirmCashCommand { get; }

    public IAsyncRelayCommand ConfirmCardCommand { get; }

    public bool IsSummaryStep => _step == CloseoutStep.Summary;

    public bool IsCashStep => _step == CloseoutStep.Cash;

    public bool IsCardConfirmStep => _step == CloseoutStep.CardConfirm;

    public bool ShowFooterBack => !IsSummaryStep;

    public string TargetTabTitle =>
        string.IsNullOrWhiteSpace(_session.TargetTabDisplayName)
            ? "No tab selected"
            : _session.TargetTabDisplayName.Trim();

    public string AmountDueText => FormatMoney(_amountDue);

    public string AmountDueLabel =>
        _session.TargetTabBalance switch
        {
            < 0m => "TOTAL DUE",
            > 0m => "CREDIT TO RETURN",
            0m => "BALANCE",
            _ => "AMOUNT",
        };

    public string CloseoutHint =>
        _session.TargetTabBalance switch
        {
            < 0m => "Collect payment to close this guest tab.",
            > 0m => "Return unused credit with cash.",
            0m => "This tab is already settled.",
            _ => "Select a guest tab on the board first.",
        };

    public bool ShowCardPayment =>
        _session.TargetTabBalance is < 0m && !IsPaymentProcessing && IsSummaryStep;

    public bool ShowPaymentButtons =>
        _session.TargetTabBalance is not 0m && IsSummaryStep;

    public bool ShowCardFeeBreakdown =>
        _session.TargetTabBalance is < 0m && _cardFee > 0m;

    public string CardFeePercentCaption =>
        $"{_cardSurchargePercent.ToString("0.##", CultureInfo.InvariantCulture)}% Square pass-through";

    public string CardFeeAmountText => FormatMoney(_cardFee);

    public string CardFeeChargeTotalText => FormatMoney(_cardChargeTotal);

    public string CardButtonCaption =>
        ShowCardFeeBreakdown ? $"Card ({CardFeeChargeTotalText})" : "Card";

    public string CardFeeWarning =>
        "Card payments include a Square surcharge. Confirm the guest accepts the charge total before sending to Square.";

    public string CashReceivedDisplay => _cashNumpad.AmountDisplay;

    public string CashTotalCaption => $"Total {AmountDueText}";

    public string CashChangeText
    {
        get
        {
            if (!_cashNumpad.TryPeekCurrency(out var received))
            {
                return "$0.00";
            }

            return FormatMoney(CalculateChange(received, _amountDue));
        }
    }

    public bool CashConfirmEnabled =>
        _cashNumpad.TryPeekCurrency(out var received) && received >= _amountDue;

    public bool CashShortWarning =>
        _cashNumpad.TryPeekCurrency(out var received) && received < _amountDue;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyPaymentState();
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
                NotifyPaymentState();
                CancelSquarePaymentCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool ShowSendingSquareOverlay => IsSendingSquare;

    public string SquareWaitingMessage
    {
        get => _squareWaitingMessage;
        private set => SetProperty(ref _squareWaitingMessage, value);
    }

    public bool IsPaymentProcessing => IsBusy || IsSendingSquare;

    public void RefreshFromSession()
    {
        UpdateAmountDue();
        OnPropertyChanged(nameof(TargetTabTitle));
        OnPropertyChanged(nameof(AmountDueText));
        OnPropertyChanged(nameof(AmountDueLabel));
        OnPropertyChanged(nameof(CloseoutHint));
        OnPropertyChanged(nameof(ShowCardPayment));
        OnPropertyChanged(nameof(ShowPaymentButtons));
        OnPropertyChanged(nameof(ShowCardFeeBreakdown));
        OnPropertyChanged(nameof(CardFeePercentCaption));
        OnPropertyChanged(nameof(CardFeeAmountText));
        OnPropertyChanged(nameof(CardFeeChargeTotalText));
        OnPropertyChanged(nameof(CardButtonCaption));
        NotifyPaymentState();
        _ = RefreshCardPricingAsync();
    }

    private void UpdateAmountDue()
    {
        _amountDue = _session.TargetTabBalance is { } bal ? Math.Abs(bal) : 0m;
    }

    private async Task RefreshCardPricingAsync()
    {
        try
        {
            var sqCfg = await _squareConfig.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            _cardSurchargePercent = sqCfg.PitstopTerminalCardSurchargePercent is > 0 and < 100
                ? sqCfg.PitstopTerminalCardSurchargePercent
                : 1.7m;

            if (_session.TargetTabBalance is { } bal && bal < 0m)
            {
                var amount = Math.Abs(bal);
                (_, _cardChargeTotal, _cardFee) = SquareCardFeeCalculator.CalculateCardTotal(amount, _cardSurchargePercent);
            }
            else
            {
                _cardFee = 0m;
                _cardChargeTotal = 0m;
            }

            OnPropertyChanged(nameof(ShowCardFeeBreakdown));
            OnPropertyChanged(nameof(CardFeePercentCaption));
            OnPropertyChanged(nameof(CardFeeAmountText));
            OnPropertyChanged(nameof(CardFeeChargeTotalText));
            OnPropertyChanged(nameof(CardButtonCaption));
        }
        catch
        {
            // Keep last-known fee settings if config load fails.
        }
    }

    private bool CanPay() =>
        !IsPaymentProcessing
        && IsSummaryStep
        && _session.TargetTabBalance is not 0m
        && !string.IsNullOrWhiteSpace(_session.TargetTabLegacyId);

    private bool CanPayCard() => CanPay() && _session.TargetTabBalance is < 0m;

    private async Task StartCashAsync()
    {
        if (!CanPay() || _session.TargetTabBalance is not { } balance)
        {
            return;
        }

        if (balance > 0m)
        {
            await PayCreditReturnCashAsync().ConfigureAwait(true);
            return;
        }

        StatusMessage = string.Empty;
        _cashNumpad.Reset(0m);
        _cashNumpad.PropertyChanged -= OnCashNumpadPropertyChanged;
        _cashNumpad.PropertyChanged += OnCashNumpadPropertyChanged;
        SetStep(CloseoutStep.Cash);
        RaiseCashUi();
    }

    private void OpenCardConfirm()
    {
        if (!CanPayCard())
        {
            return;
        }

        StatusMessage = string.Empty;
        SetStep(CloseoutStep.CardConfirm);
    }

    private async Task ConfirmCashAsync()
    {
        if (_step != CloseoutStep.Cash || !_cashNumpad.TryPeekCurrency(out var received))
        {
            return;
        }

        received = decimal.Round(received, 2, MidpointRounding.AwayFromZero);
        if (received < _amountDue)
        {
            StatusMessage = "Tendered is less than the amount due.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_session.TargetTabLegacyId))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var r = await _funds
                .CommitFundMovementAsync(
                    _session.TargetTabLegacyId!,
                    "cash",
                    _amountDue,
                    "Guest closeout - cash payment",
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Could not record payment.";
                return;
            }

            RegisterUndo(r.FundCommitBatchId);
            await FinishCloseoutAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PayCreditReturnCashAsync()
    {
        if (_session.TargetTabBalance is not { } balance || balance <= 0m)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var defaultAmount = balance;
            var entered = await _inputOverlay
                .ShowNumpadAsync(defaultAmount, $"Return credit - {TargetTabTitle}", allowSignedAmount: false, CancellationToken.None)
                .ConfigureAwait(true);
            if (entered is null)
            {
                return;
            }

            var amt = decimal.Round(entered.Value, 2, MidpointRounding.AwayFromZero);
            if (amt <= 0m)
            {
                StatusMessage = "Enter a positive amount.";
                return;
            }

            var draw = decimal.Min(amt, balance);
            var r = await _funds
                .CommitFundMovementAsync(
                    _session.TargetTabLegacyId!,
                    "manual",
                    -draw,
                    "Guest closeout - settle tab credit",
                    CancellationToken.None)
                .ConfigureAwait(true);

            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Could not record payment.";
                return;
            }

            RegisterUndo(r.FundCommitBatchId);
            await FinishCloseoutAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConfirmCardAsync()
    {
        if (_step != CloseoutStep.CardConfirm || _session.TargetTabBalance is not { } balance || balance >= 0m)
        {
            return;
        }

        var amount = Math.Abs(balance);
        var tabName = TargetTabTitle;
        var tabLegacy = _session.TargetTabLegacyId!;

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            await RefreshCardPricingAsync().ConfigureAwait(true);
            var feePercent = _cardSurchargePercent;
            var sqCfg = await _squareConfig.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            var catalogVariationId = sqCfg.BarTabCardCatalogVariationId?.Trim() ?? string.Empty;
            var checkout = SquareTabCardCheckoutBuilder.Build(
                amount,
                feePercent,
                "Guest tab closeout",
                "Bar Tab - Guest Closeout",
                catalogVariationId,
                "Guest tab closeout - " + tabName,
                "GuestCloseout",
                tabName);

            SquareWaitingMessage = SquareTabCardCheckoutBuilder.FormatWaitingMessage(
                string.Empty,
                checkout.ChargeTotal,
                checkout.CardFee);
            IsSendingSquare = true;
            DisposeSquareChargeCts();
            _squareChargeCts = new CancellationTokenSource();
            SquareCardPaymentOutcome cardOutcome;
            try
            {
                cardOutcome = await _squarePayments.PresentAndLogAsync(
                    SquareTabCardCheckoutBuilder.ToOrchestratorRequest(checkout, tabLegacy),
                    _squareChargeCts.Token).ConfigureAwait(true);
            }
            finally
            {
                IsSendingSquare = false;
                DisposeSquareChargeCts();
            }

            if (!cardOutcome.Approved)
            {
                StatusMessage = cardOutcome.DeclineReason ?? PitstopSaleCommitHelper.PaymentNotRecordedMessage;
                return;
            }

            if (!cardOutcome.AlreadyRecorded)
            {
                var squareResult = await _funds
                    .CommitSquareCardTopUpAsync(
                        tabLegacy,
                        amount,
                        "Guest closeout - card payment",
                        new SquarePaymentCommitMetadata
                        {
                            IdempotencyKey = cardOutcome.IdempotencyKey,
                            SquarePaymentId = cardOutcome.SquarePaymentId ?? cardOutcome.IdempotencyKey,
                            SquareCheckoutId = cardOutcome.SquareCheckoutId,
                            BaseAmount = amount,
                            SurchargeAmount = checkout.CardFee,
                            ChargedAmount = checkout.ChargeTotal,
                            PaymentAttemptId = cardOutcome.AttemptId,
                        },
                        CancellationToken.None)
                    .ConfigureAwait(true);

                if (!squareResult.Ok)
                {
                    StatusMessage = squareResult.ErrorMessage
                        ?? "Square approved but the POS could not record the payment.";
                    return;
                }

                RegisterUndo(squareResult.FundCommitBatchId);
            }

            await FinishCloseoutAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RegisterUndo(string? batchId)
    {
        if (string.IsNullOrEmpty(batchId) || string.IsNullOrWhiteSpace(_session.TargetTabLegacyId))
        {
            return;
        }

        var tabLegacy = _session.TargetTabLegacyId!;
        var label = TargetTabTitle;
        _undo.PushUndo(
            "Undo guest closeout (" + label + ")",
            async () =>
            {
                var rev = await _funds
                    .ReverseFundCommitAsync(tabLegacy, batchId!, CancellationToken.None)
                    .ConfigureAwait(true);
                if (!rev.Ok)
                {
                    return false;
                }

                var restore = await _tabs
                    .RestoreSoftDeletedTabAsync(tabLegacy, CancellationToken.None)
                    .ConfigureAwait(true);
                if (!restore.Ok)
                {
                    return false;
                }

                _refreshBus.RequestRefresh();
                return true;
            });
    }

    private async Task FinishCloseoutAsync()
    {
        if (!string.IsNullOrWhiteSpace(_session.TargetTabLegacyId))
        {
            var del = await _tabs
                .SoftDeleteTabAsync(_session.TargetTabLegacyId!, CancellationToken.None)
                .ConfigureAwait(true);
            if (!del.Ok)
            {
                StatusMessage = del.ErrorMessage ?? "Payment recorded but the guest tab could not be removed.";
                _refreshBus.RequestRefresh();
                _inputOverlay.Close();
                _slide.Close();
                _session.Clear();
                return;
            }
        }

        _refreshBus.RequestRefresh();
        _inputOverlay.Close();
        _slide.Close();
        _session.Clear();
    }

    private void CancelOrClosePanel()
    {
        if (IsSendingSquare)
        {
            CancelSquarePayment();
            return;
        }

        if (_step != CloseoutStep.Summary)
        {
            GoBack();
            return;
        }

        _inputOverlay.Close();
        _slide.Close();
        _session.Clear();
    }

    private void GoBack()
    {
        if (_step == CloseoutStep.Cash)
        {
            _cashNumpad.PropertyChanged -= OnCashNumpadPropertyChanged;
        }

        StatusMessage = string.Empty;
        SetStep(CloseoutStep.Summary);
    }

    private void SetStep(CloseoutStep step)
    {
        _step = step;
        OnPropertyChanged(nameof(IsSummaryStep));
        OnPropertyChanged(nameof(IsCashStep));
        OnPropertyChanged(nameof(IsCardConfirmStep));
        OnPropertyChanged(nameof(ShowFooterBack));
        OnPropertyChanged(nameof(ShowCardPayment));
        OnPropertyChanged(nameof(ShowPaymentButtons));
        NotifyPaymentState();
    }

    private void OnCashNumpadPropertyChanged(object? sender, PropertyChangedEventArgs e)
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
        OnPropertyChanged(nameof(CashTotalCaption));
        OnPropertyChanged(nameof(CashConfirmEnabled));
        OnPropertyChanged(nameof(CashShortWarning));
        ConfirmCashCommand.NotifyCanExecuteChanged();
    }

    private void CancelSquarePayment() => _squareChargeCts?.Cancel();

    private void DisposeSquareChargeCts()
    {
        _squareChargeCts?.Cancel();
        _squareChargeCts?.Dispose();
        _squareChargeCts = null;
    }

    private void NotifyPaymentState()
    {
        OnPropertyChanged(nameof(IsPaymentProcessing));
        OnPropertyChanged(nameof(ShowCardPayment));
        OnPropertyChanged(nameof(ShowPaymentButtons));
        PayCashCommand.NotifyCanExecuteChanged();
        PayCardCommand.NotifyCanExecuteChanged();
        ConfirmCashCommand.NotifyCanExecuteChanged();
        ConfirmCardCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private static decimal CalculateChange(decimal received, decimal amountDue) =>
        decimal.Round(received - amountDue, 2, MidpointRounding.AwayFromZero);

    private static string FormatMoney(decimal value) =>
        "$" + value.ToString("0.00", CultureInfo.InvariantCulture);
}
