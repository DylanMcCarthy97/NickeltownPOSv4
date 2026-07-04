using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Payments;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Services.Settings;
using Windows.UI;

namespace NickeltownPOSV4.ViewModels;

public sealed class FundTypeTileModel : ObservableObject
{
    private static readonly SolidColorBrush RegularIdleBg = new(Color.FromArgb(255, 248, 251, 255));
    private static readonly SolidColorBrush RegularSelectedBg = new(Color.FromArgb(255, 227, 238, 255));
    private static readonly SolidColorBrush RegularIdleBorder = new(Color.FromArgb(255, 211, 224, 248));
    private static readonly SolidColorBrush RegularSelectedBorder = new(Color.FromArgb(255, 30, 79, 214));

    private bool _isSelected;

    public FundTypeTileModel(string key, string displayName, string emoji, bool isSquareBrand = false)
    {
        Key = key;
        DisplayName = displayName;
        Emoji = emoji;
        IsSquareBrand = isSquareBrand;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string Emoji { get; }
    public bool IsSquareBrand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TileBackgroundBrush));
            OnPropertyChanged(nameof(TileBorderBrush));
            OnPropertyChanged(nameof(TileBorderThickness));
        }
    }

    /// <summary>Square uses the same light tile chrome as other types so labels stay readable; branding is the logo chip only.</summary>
    public Brush TileBackgroundBrush =>
        _isSelected ? RegularSelectedBg : RegularIdleBg;

    public Brush TileBorderBrush =>
        _isSelected ? RegularSelectedBorder : RegularIdleBorder;

    public Thickness TileBorderThickness => new(_isSelected ? 2.5 : 1);
}

public sealed class AddFundsPanelViewModel : ObservableViewModel
{
    private readonly ITabFundsService _funds;
    private readonly IAddFundsSession _session;
    private readonly ITabWorkspaceRefreshBus _refreshBus;
    private readonly ISlidePanelService _slide;
    private readonly IInputOverlayService _inputOverlay;
    private readonly ITabWorkspaceUndoStack _undo;
    private readonly ISquareCardPaymentOrchestrator _squarePayments;
    private readonly ISquareConfigService _squareConfig;
    private readonly IUserSessionService _userSession;
    private readonly IAuditLogService _audit;
    private readonly IWindowHandleProvider _windowHandle;
    private readonly IAuthenticationService _auth;

    private FundTypeTileModel? _selectedTile;
    private string _amountText = string.Empty;
    private string _notes = string.Empty;
    private string _detailHint = "Select a transaction type, enter amount, and apply.";
    private string _targetTabTitle = "No tab selected";
    private string _currentBalanceText = "Current balance: -";
    private string _statusMessage = string.Empty;
    private string? _successBanner;
    private bool _isBusy;
    private bool _isSendingSquare;
    private string _squareWaitingMessage = string.Empty;
    private CancellationTokenSource? _squareChargeCts;

    public AddFundsPanelViewModel(
        ITabFundsService funds,
        IAddFundsSession session,
        ITabWorkspaceRefreshBus refreshBus,
        ISlidePanelService slide,
        IInputOverlayService inputOverlay,
        ITabWorkspaceUndoStack undo,
        ISquareCardPaymentOrchestrator squarePayments,
        ISquareConfigService squareConfig,
        IUserSessionService userSession,
        IAuditLogService audit,
        IWindowHandleProvider windowHandle,
        IAuthenticationService auth)
    {
        _funds = funds;
        _session = session;
        _refreshBus = refreshBus;
        _slide = slide;
        _inputOverlay = inputOverlay;
        _undo = undo;
        _squarePayments = squarePayments;
        _squareConfig = squareConfig;
        _userSession = userSession;
        _audit = audit;
        _windowHandle = windowHandle;
        _auth = auth;

        foreach (var type in BuildTypes())
        {
            Types.Add(type);
        }

        CancelCommand = new RelayCommand(CancelOrClosePanel);
        CancelSquarePaymentCommand = new RelayCommand(CancelSquarePayment, () => IsSendingSquare);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync, CanApply);
        BeginAmountEntryCommand = new AsyncRelayCommand(BeginAmountEntryAsync, () => CanEditFields);
        BeginNotesEntryCommand = new AsyncRelayCommand(BeginNotesEntryAsync, () => CanEditFields);

        RefreshHeader();
    }

    public ObservableCollection<FundTypeTileModel> Types { get; } = new();

    public FundTypeTileModel? SelectedTile
    {
        get => _selectedTile;
        set
        {
            if (!SetProperty(ref _selectedTile, value))
            {
                return;
            }

            foreach (var t in Types)
            {
                t.IsSelected = ReferenceEquals(t, _selectedTile);
            }

            DetailHint = _selectedTile?.Key switch
            {
                "cash" => "Cash posts immediately to tab balance and history.",
                "square" => "Square Terminal charges the card total (includes pass-through fee, rounded to 5Â¢). The amount you enter is credited to the tab when approved.",
                "raffle" => "Raffle payout posted as a money movement.",
                "reimburse" => "Reimbursement posted as a money movement.",
                "manual" => "Manual adjustment: amount may be positive or negative.",
                "correction" => "Correction: amount may be positive or negative.",
                _ => "Select a transaction type, enter amount, and apply.",
            };
            NotifyFooterHint();
            StatusMessage = string.Empty;
            SuccessBanner = null;
            OnPropertyChanged(nameof(SupportsSignedAmount));
            OnPropertyChanged(nameof(AmountDisplay));
            ApplyCommand.NotifyCanExecuteChanged();
            BeginAmountEntryCommand.NotifyCanExecuteChanged();
        }
    }

    public bool SupportsSignedAmount => SelectedTile?.Key is "manual" or "correction";

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
            {
                OnPropertyChanged(nameof(AmountDisplay));
                NotifyFooterHint();
                ApplyCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string AmountDisplay =>
        TryParseAmount(AmountText, SupportsSignedAmount, out var value)
            ? FormatMoney(value)
            : "$0.00";

    public string FooterHint =>
        TryParseAmount(AmountText, SupportsSignedAmount, out var value) && value != 0m
            ? $"Confirm: {FormatMoney(value)} -> {TargetTabTitle}"
            : DetailHint;

    public bool IsPaymentProcessing => IsBusy || IsSendingSquare;

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string DetailHint
    {
        get => _detailHint;
        private set => SetProperty(ref _detailHint, value);
    }

    public string TargetTabTitle
    {
        get => _targetTabTitle;
        private set => SetProperty(ref _targetTabTitle, value);
    }

    public string CurrentBalanceText
    {
        get => _currentBalanceText;
        private set => SetProperty(ref _currentBalanceText, value);
    }

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

    public string? SuccessBanner
    {
        get => _successBanner;
        private set
        {
            if (SetProperty(ref _successBanner, value))
            {
                OnPropertyChanged(nameof(HasSuccessBanner));
            }
        }
    }

    public bool HasSuccessBanner => !string.IsNullOrWhiteSpace(SuccessBanner);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanEditFields));
            OnPropertyChanged(nameof(IsPaymentProcessing));
            BeginAmountEntryCommand.NotifyCanExecuteChanged();
            BeginNotesEntryCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    public bool CanEditFields => !IsPaymentProcessing;

    public bool IsSendingSquare
    {
        get => _isSendingSquare;
        private set
        {
            if (!SetProperty(ref _isSendingSquare, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowSendingSquareOverlay));
            OnPropertyChanged(nameof(IsPaymentProcessing));
            CancelSquarePaymentCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            ApplyCommand.NotifyCanExecuteChanged();
        }
    }

    public bool ShowSendingSquareOverlay => IsSendingSquare;

    public string SquareWaitingMessage
    {
        get => _squareWaitingMessage;
        private set => SetProperty(ref _squareWaitingMessage, value);
    }

    public IRelayCommand CancelCommand { get; }
    public IRelayCommand CancelSquarePaymentCommand { get; }
    public IAsyncRelayCommand ApplyCommand { get; }
    public IAsyncRelayCommand BeginAmountEntryCommand { get; }
    public IAsyncRelayCommand BeginNotesEntryCommand { get; }

    private void RefreshHeader()
    {
        if (string.IsNullOrWhiteSpace(_session.TargetTabLegacyId))
        {
            TargetTabTitle = "No tab selected";
            CurrentBalanceText = "Select a tab on the board first.";
            NotifyFooterHint();
            return;
        }

        var name = string.IsNullOrWhiteSpace(_session.TargetTabDisplayName)
            ? _session.TargetTabLegacyId
            : _session.TargetTabDisplayName;
        TargetTabTitle = $"Tab: {name}";

        if (_session.TargetTabBalance is not { } bal)
        {
            CurrentBalanceText = "Current balance: -";
            NotifyFooterHint();
            return;
        }

        var abs = Math.Abs(bal).ToString("0.00", CultureInfo.InvariantCulture);
        var prefix = bal < 0m ? "-" : string.Empty;
        CurrentBalanceText = $"Current balance: {prefix}${abs}";
        NotifyFooterHint();
    }

    private void NotifyFooterHint() => OnPropertyChanged(nameof(FooterHint));

    private async Task BeginAmountEntryAsync()
    {
        if (!CanEditFields)
        {
            return;
        }

        SuccessBanner = null;
        var allowSigned = SupportsSignedAmount;
        var initial = TryParseAmount(AmountText, allowSigned, out var parsed) ? parsed : 0m;
        var result = await _inputOverlay
            .ShowNumpadAsync(initial, "Enter Amount", allowSigned, CancellationToken.None)
            .ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        AmountText = decimal.Round(result.Value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture).TrimStart('+');
    }

    private async Task BeginNotesEntryAsync()
    {
        if (!CanEditFields)
        {
            return;
        }

        SuccessBanner = null;
        var result = await _inputOverlay.ShowKeyboardAsync(Notes, "Type Note", CancellationToken.None).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        Notes = result.Trim();
    }

    private bool CanApply() =>
        !IsPaymentProcessing
        && _selectedTile is not null
        && TryParseAmount(AmountText, SupportsSignedAmount, out var amount)
        && amount != 0m
        && (SupportsSignedAmount || amount > 0m)
        && !string.IsNullOrWhiteSpace(_session.TargetTabLegacyId);

    private async Task ApplyAsync()
    {
        if (_selectedTile is null || !TryParseAmount(AmountText, SupportsSignedAmount, out var amount))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_session.TargetTabLegacyId))
        {
            StatusMessage = "Select a tab on the board before adding funds.";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        SuccessBanner = null;
        try
        {
            if (_selectedTile.Key == "square")
            {
                var tabName = string.IsNullOrWhiteSpace(_session.TargetTabDisplayName)
                    ? _session.TargetTabLegacyId!
                    : _session.TargetTabDisplayName.Trim();

                var sqCfg = await _squareConfig.LoadAsync(CancellationToken.None).ConfigureAwait(true);
                var feePercent = sqCfg.PitstopTerminalCardSurchargePercent is > 0 and < 100
                    ? sqCfg.PitstopTerminalCardSurchargePercent
                    : 1.7m;
                var catalogVariationId = sqCfg.BarTabCardCatalogVariationId?.Trim() ?? string.Empty;
                var checkout = SquareTabCardCheckoutBuilder.Build(
                    amount,
                    feePercent,
                    "Bar Top-Up",
                    "Bar Tab",
                    catalogVariationId,
                    $"Bar Top-Up - {tabName}",
                    "BarTopUp",
                    tabName);

                SquareWaitingMessage = SquareTabCardCheckoutBuilder.FormatWaitingMessage(
                    "Processing card payment...",
                    checkout.ChargeTotal,
                    checkout.CardFee);
                IsSendingSquare = true;
                IsBusy = true;
                DisposeSquareChargeCts();
                _squareChargeCts = new CancellationTokenSource();
                SquareCardPaymentOutcome cardOutcome;
                try
                {
                    cardOutcome = await _squarePayments.PresentAndLogAsync(
                        SquareTabCardCheckoutBuilder.ToOrchestratorRequest(checkout, _session.TargetTabLegacyId!),
                        _squareChargeCts.Token).ConfigureAwait(true);
                }
                finally
                {
                    IsSendingSquare = false;
                    IsBusy = false;
                    DisposeSquareChargeCts();
                }

                if (!cardOutcome.Approved)
                {
                    StatusMessage = cardOutcome.DeclineReason ?? PitstopSaleCommitHelper.PaymentNotRecordedMessage;
                    return;
                }

                if (cardOutcome.AlreadyRecorded)
                {
                    SuccessBanner = $"Recorded: {FormatMoney(amount)} card for {TargetTabTitle}.";
                    _refreshBus.RequestRefresh();
                    _slide.Close();
                    _session.Clear();
                    return;
                }

                var squareResult = await _funds
                    .CommitSquareCardTopUpAsync(
                        _session.TargetTabLegacyId!,
                        amount,
                        Notes,
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
                        ?? "Square approved but the POS could not record the top-up - check Square dashboard and reconcile manually.";
                    return;
                }

                var sqTabLegacy = _session.TargetTabLegacyId!;
                var sqBatchId = squareResult.FundCommitBatchId;
                var sqTileLabel = _selectedTile.DisplayName;

                SuccessBanner = $"Recorded: {FormatMoney(amount)} card for {TargetTabTitle}.";
                await Task.Delay(480).ConfigureAwait(true);
                SuccessBanner = null;

                _refreshBus.RequestRefresh();
                _slide.Close();
                _session.Clear();

                if (!string.IsNullOrEmpty(sqBatchId))
                {
                    var sqAmount = amount;
                    _undo.PushUndo(
                        $"Undo Square card ({sqTileLabel}) - POS only, no Square refund",
                        () => RunSquareTopUpUndoAsync(sqTabLegacy, sqBatchId!, sqAmount));
                }

                return;
            }

            var result = await _funds
                .CommitFundMovementAsync(_session.TargetTabLegacyId!, _selectedTile.Key, amount, Notes, CancellationToken.None)
                .ConfigureAwait(true);

            if (!result.Ok)
            {
                StatusMessage = result.ErrorMessage ?? "Could not record this movement.";
                return;
            }

            var tabLegacy = _session.TargetTabLegacyId!;
            var batchId = result.FundCommitBatchId;
            var tileLabel = _selectedTile.DisplayName;

            SuccessBanner = $"Recorded: {FormatMoney(amount)} {_selectedTile.Key} for {TargetTabTitle}.";
            await Task.Delay(480).ConfigureAwait(true);
            SuccessBanner = null;

            _refreshBus.RequestRefresh();
            _slide.Close();
            _session.Clear();

            if (!string.IsNullOrEmpty(batchId))
            {
                _undo.PushUndo(
                    $"Undo last funds ({tileLabel})",
                    async () =>
                    {
                        var rev = await _funds
                            .ReverseFundCommitAsync(tabLegacy, batchId!, CancellationToken.None)
                            .ConfigureAwait(true);
                        if (!rev.Ok)
                        {
                            return false;
                        }

                        _refreshBus.RequestRefresh();
                        return true;
                    });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RunSquareTopUpUndoAsync(string tabLegacyId, string commitBatchId, decimal amount)
    {
        if (!_userSession.IsManager)
        {
            try
            {
                await _audit.LogAsync(
                    AuditActions.PermissionDenied,
                    AuditEntityTypes.Tab,
                    entityId: tabLegacyId,
                    amount: amount,
                    reason: "Undo Square card top-up requires Admin/Treasurer.",
                    success: false).ConfigureAwait(true);
            }
            catch
            {
                // audit never blocks
            }

            await ShowSquareUndoMessageAsync(
                "Admin or Treasurer access is required to undo a Square card top-up.").ConfigureAwait(true);
            return false;
        }

        var proceed = await ShowSquareUndoWarningAsync(amount).ConfigureAwait(true);
        if (!proceed)
        {
            return false;
        }

        var pin = await _inputOverlay
            .ShowPinNumpadAsync("Enter Admin/Treasurer PIN to undo Square top-up", 4, true, CancellationToken.None)
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(pin))
        {
            return false;
        }

        var auth = await _auth.AuthenticateByPinAsync(pin).ConfigureAwait(true);
        if (!auth.Ok || !IsManagerRole(auth.Role))
        {
            try
            {
                await _audit.LogAsync(
                    AuditActions.PermissionDenied,
                    AuditEntityTypes.Tab,
                    entityId: tabLegacyId,
                    amount: amount,
                    reason: "Square undo PIN did not match an Admin/Treasurer.",
                    success: false).ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }

            await ShowSquareUndoMessageAsync("PIN was not accepted for Admin/Treasurer.").ConfigureAwait(true);
            return false;
        }

        var reason = await _inputOverlay
            .ShowKeyboardAsync(string.Empty, "Reason for undoing Square top-up", CancellationToken.None)
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(reason))
        {
            await ShowSquareUndoMessageAsync("A reason is required to undo a Square top-up.").ConfigureAwait(true);
            return false;
        }

        try
        {
            await _audit.LogAsync(
                AuditActions.TabFundsUndoneSquareWarning,
                AuditEntityTypes.Tab,
                entityId: tabLegacyId,
                amount: amount,
                reason: $"Undo confirmed by {auth.DisplayName}: {reason.Trim()}").ConfigureAwait(true);
        }
        catch
        {
            // ignore audit failures
        }

        var rev = await _funds
            .ReverseFundCommitAsync(tabLegacyId, commitBatchId, CancellationToken.None)
            .ConfigureAwait(true);

        if (!rev.Ok)
        {
            try
            {
                await _audit.LogAsync(
                    AuditActions.TabFundsUndone,
                    AuditEntityTypes.Tab,
                    entityId: tabLegacyId,
                    amount: amount,
                    reason: rev.ErrorMessage ?? "Square undo failed.",
                    success: false).ConfigureAwait(true);
            }
            catch
            {
                // ignore audit failures
            }

            return false;
        }

        try
        {
            await _audit.LogAsync(
                AuditActions.TabFundsUndone,
                AuditEntityTypes.Tab,
                entityId: tabLegacyId,
                amount: amount,
                reason: $"Square top-up reversed by {auth.DisplayName}: {reason.Trim()} - POS only, Square not refunded.").ConfigureAwait(true);
        }
        catch
        {
            // ignore audit failures
        }

        _refreshBus.RequestRefresh();
        return true;
    }

    private static bool IsManagerRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        var r = role.Trim().ToLowerInvariant();
        return r is "admin" or "manager" or "treasurer" or "owner";
    }

    private async Task<bool> ShowSquareUndoWarningAsync(decimal amount)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return false;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Undo Square card top-up?",
            Content = new TextBlock
            {
                Text =
                    $"You are about to remove a {FormatMoney(amount)} Square card top-up from the POS record only.\n\n"
                    + "This DOES NOT refund the Square payment. Refunds must be handled in Square.\n\n"
                    + "Continue only if you understand the Square charge will remain until you refund it in Square.",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            },
            PrimaryButtonText = "Undo POS record",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        var res = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        return res == ContentDialogResult.Primary;
    }

    private async Task ShowSquareUndoMessageAsync(string text)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Square top-up undo",
            Content = new TextBlock
            {
                Text = text,
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords,
            },
            CloseButtonText = "OK",
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        await dlg.ShowAsync().AsTask().ConfigureAwait(true);
    }

    private void CancelOrClosePanel()
    {
        if (IsSendingSquare)
        {
            CancelSquarePayment();
            return;
        }

        ClosePanel();
    }

    private void CancelSquarePayment()
    {
        if (!IsSendingSquare)
        {
            return;
        }

        _squareChargeCts?.Cancel();
    }

    private void DisposeSquareChargeCts()
    {
        _squareChargeCts?.Dispose();
        _squareChargeCts = null;
    }

    private void ClosePanel()
    {
        if (IsSendingSquare)
        {
            return;
        }

        _inputOverlay.Close();
        _slide.Close();
        _session.Clear();
    }

    private static bool TryParseAmount(string? text, bool allowNegative, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(text) || text == "-")
        {
            return false;
        }

        var t = text.Trim().Replace("$", string.Empty).Replace("âˆ’", "-");
        if (!decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
            && !decimal.TryParse(t, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            return false;
        }

        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (!allowNegative && amount < 0m)
        {
            return false;
        }

        return true;
    }

    private static string FormatMoney(decimal value)
    {
        var inv = CultureInfo.InvariantCulture;
        var abs = Math.Abs(value).ToString("0.00", inv);
        return value < 0m ? "-$" + abs : "$" + abs;
    }

    private static FundTypeTileModel[] BuildTypes() =>
        new[]
        {
            new FundTypeTileModel("cash", "Cash", "\U0001F4B5"),
            new FundTypeTileModel("square", "Square Card", "\U0001F4B3", true),
            new FundTypeTileModel("raffle", "Raffle", "\U0001F39F\uFE0F"),
            new FundTypeTileModel("reimburse", "Reimburse", "\U0001F9FE"),
            new FundTypeTileModel("manual", "Adjustment", "\U0001F6E0\uFE0F"),
            new FundTypeTileModel("correction", "Correction", "\u21BA"),
        };
}
