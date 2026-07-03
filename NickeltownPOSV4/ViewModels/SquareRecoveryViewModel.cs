using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;

namespace NickeltownPOSV4.ViewModels;

public sealed class SquareRecoveryViewModel : ObservableViewModel
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    private readonly ISquareRecoveryRepository _recovery;
    private readonly IUserSessionService _session;
    private readonly INavigationService _navigation;
    private readonly IAuditLogService _audit;
    private readonly IWindowHandleProvider _windowHandle;
    private readonly IInputOverlayService _input;
    private readonly IAuthenticationService _auth;

    private readonly IPitstopPaymentRecoveryService _paymentRecovery;

    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public SquareRecoveryViewModel(
        ISquareRecoveryRepository recovery,
        IUserSessionService session,
        INavigationService navigation,
        IAuditLogService audit,
        IWindowHandleProvider windowHandle,
        IInputOverlayService input,
        IAuthenticationService auth,
        IPitstopPaymentRecoveryService paymentRecovery)
    {
        _recovery = recovery;
        _session = session;
        _navigation = navigation;
        _audit = audit;
        _windowHandle = windowHandle;
        _input = input;
        _auth = auth;
        _paymentRecovery = paymentRecovery;
        Rows = new PagedCollection<SquareRecoveryRowVm>(pageSize: 5);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        BackCommand = new RelayCommand(() => _navigation.TryGoBack());
    }

    public PagedCollection<SquareRecoveryRowVm> Rows { get; }

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
        if (!_session.CanAccessTreasurer)
        {
            await _audit
                .LogAsync(
                    AuditActions.PermissionDenied,
                    AuditEntityTypes.System,
                    entityId: "SquareRecovery",
                    reason: "Staff attempted to open Square Recovery.")
                .ConfigureAwait(true);
            StatusMessage = "Admin/Treasurer access required.";
            _navigation.TryGoBack();
            return;
        }

        await LoadAsync().ConfigureAwait(true);
    }

    public async Task LoadAsync()
    {
        if (!_session.CanAccessTreasurer)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading unresolved Square payments...";
            var rows = await _recovery.GetOrphanAttemptsAsync().ConfigureAwait(true);
            Rows.Replace(rows.Select(r => new SquareRecoveryRowVm(r, this)));
            StatusMessage = Rows.TotalCount == 0
                ? "No unresolved Square payments."
                : $"{Rows.TotalCount} unresolved Square payment(s).";
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

    internal async Task RecoverPitstopSaleAsync(SquareRecoveryRowVm row)
    {
        if (!row.HasRecoverablePayload)
        {
            StatusMessage = "This payment has no automatic recovery payload — link manually or mark reconciled.";
            return;
        }

        if (!await EnsureManagerPinAsync("Recover Pitstop sale from Square payment").ConfigureAwait(true))
        {
            return;
        }

        var result = await _paymentRecovery.RecoverPitstopSaleAsync(row.AttemptId).ConfigureAwait(true);
        if (!result.Ok)
        {
            StatusMessage = result.ErrorMessage ?? "Recovery failed.";
            return;
        }

        await _audit
            .LogAsync(
                AuditActions.PaymentSaleSaved,
                AuditEntityTypes.PitstopSale,
                row.AttemptId.ToString(CultureInfo.InvariantCulture),
                row.ChargedAmount,
                reason: "Recovered via Square Recovery page.")
            .ConfigureAwait(true);

        await LoadAsync().ConfigureAwait(true);
        StatusMessage = $"Recovered Pitstop sale for attempt #{row.AttemptId}.";
    }

    internal async Task IgnoreRecoveryAsync(SquareRecoveryRowVm row)
    {
        if (!await EnsureManagerPinAsync("Ignore Square recovery").ConfigureAwait(true))
        {
            return;
        }

        var result = await _paymentRecovery.IgnoreAsync(row.AttemptId, "Ignored from Square Recovery").ConfigureAwait(true);
        if (!result.Ok)
        {
            StatusMessage = result.ErrorMessage ?? "Could not ignore.";
            return;
        }

        await LoadAsync().ConfigureAwait(true);
        StatusMessage = "Recovery ignored.";
    }

    internal async Task MarkManuallyReconciledAsync(SquareRecoveryRowVm row)
    {
        if (!await EnsureManagerPinAsync("Mark Square payment manually reconciled").ConfigureAwait(true))
        {
            return;
        }

        var note = await PromptForReasonAsync(
                "Reconciliation note",
                "Why are you marking this Square payment as reconciled? (required)")
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        var result = await _recovery.MarkManuallyReconciledAsync(row.AttemptId, note).ConfigureAwait(true);
        if (!result.Ok)
        {
            StatusMessage = result.ErrorMessage ?? "Could not mark reconciled.";
            return;
        }

        await _audit
            .LogAsync(
                AuditActions.SquareRecoveryManuallyReconciled,
                AuditEntityTypes.SquareAttempt,
                row.AttemptId.ToString(CultureInfo.InvariantCulture),
                row.ChargedAmount,
                reason: note,
                detailsJson: BuildDetailsJson(row))
            .ConfigureAwait(true);

        await LoadAsync().ConfigureAwait(true);
        StatusMessage = "Square payment marked as manually reconciled.";
    }

    internal async Task AddNoteAsync(SquareRecoveryRowVm row)
    {
        if (!_session.CanAccessTreasurer)
        {
            return;
        }

        var note = await PromptForReasonAsync(
                "Add note",
                "Add a note to this Square payment record.")
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        var result = await _recovery.AddNoteAsync(row.AttemptId, note).ConfigureAwait(true);
        if (!result.Ok)
        {
            StatusMessage = result.ErrorMessage ?? "Could not add note.";
            return;
        }

        await _audit
            .LogAsync(
                AuditActions.SquareRecoveryNoteAdded,
                AuditEntityTypes.SquareAttempt,
                row.AttemptId.ToString(CultureInfo.InvariantCulture),
                reason: note)
            .ConfigureAwait(true);

        await LoadAsync().ConfigureAwait(true);
        StatusMessage = "Note saved.";
    }

    internal async Task LinkPitstopAsync(SquareRecoveryRowVm row)
    {
        if (!await EnsureManagerPinAsync("Link Square payment to Pitstop sale").ConfigureAwait(true))
        {
            return;
        }

        var saleIdText = await _input
            .ShowKeyboardAsync(string.Empty, "Pitstop sale Id", CancellationToken.None)
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(saleIdText)
            || !long.TryParse(saleIdText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var saleId))
        {
            StatusMessage = "Enter a numeric Pitstop sale Id.";
            return;
        }

        var note = await PromptForReasonAsync(
                "Link to Pitstop sale",
                "Optional note for the audit trail.")
            .ConfigureAwait(true);

        var result = await _recovery.LinkPitstopSaleAsync(row.AttemptId, saleId, note).ConfigureAwait(true);
        if (!result.Ok)
        {
            StatusMessage = result.ErrorMessage ?? "Could not link the Pitstop sale.";
            return;
        }

        await _audit
            .LogAsync(
                AuditActions.SquareRecoveryLinkedPitstop,
                AuditEntityTypes.SquareAttempt,
                row.AttemptId.ToString(CultureInfo.InvariantCulture),
                row.ChargedAmount,
                reason: note,
                detailsJson: BuildDetailsJson(row, ("linkedSaleId", saleId.ToString(CultureInfo.InvariantCulture))))
            .ConfigureAwait(true);

        await LoadAsync().ConfigureAwait(true);
        StatusMessage = $"Linked attempt #{row.AttemptId} to Pitstop sale {saleId}.";
    }

    internal async Task LinkTabAsync(SquareRecoveryRowVm row)
    {
        if (!await EnsureManagerPinAsync("Link Square payment to tab top-up").ConfigureAwait(true))
        {
            return;
        }

        var paymentIdText = await _input
            .ShowKeyboardAsync(string.Empty, "Local tab payment Id", CancellationToken.None)
            .ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(paymentIdText)
            || !long.TryParse(paymentIdText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var paymentId))
        {
            StatusMessage = "Enter a numeric Payments.Id.";
            return;
        }

        var note = await PromptForReasonAsync(
                "Link to tab payment",
                "Optional note for the audit trail.")
            .ConfigureAwait(true);

        var result = await _recovery.LinkTabPaymentAsync(row.AttemptId, paymentId, note).ConfigureAwait(true);
        if (!result.Ok)
        {
            StatusMessage = result.ErrorMessage ?? "Could not link the tab payment.";
            return;
        }

        await _audit
            .LogAsync(
                AuditActions.SquareRecoveryLinkedTab,
                AuditEntityTypes.SquareAttempt,
                row.AttemptId.ToString(CultureInfo.InvariantCulture),
                row.ChargedAmount,
                reason: note,
                detailsJson: BuildDetailsJson(row, ("linkedPaymentId", paymentId.ToString(CultureInfo.InvariantCulture))))
            .ConfigureAwait(true);

        await LoadAsync().ConfigureAwait(true);
        StatusMessage = $"Linked attempt #{row.AttemptId} to payment {paymentId}.";
    }

    private async Task<bool> EnsureManagerPinAsync(string actionLabel)
    {
        if (!_session.CanAccessTreasurer)
        {
            return false;
        }

        var pin = await _input
            .ShowKeyboardAsync(string.Empty, $"{actionLabel} — enter Admin/Treasurer PIN", CancellationToken.None)
            .ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(pin))
        {
            StatusMessage = "Action cancelled (no PIN entered).";
            return false;
        }

        var auth = await _auth.AuthenticateByPinAsync(pin.Trim()).ConfigureAwait(true);
        if (!auth.Ok)
        {
            await _audit
                .LogAsync(
                    AuditActions.PermissionDenied,
                    AuditEntityTypes.System,
                    entityId: "SquareRecovery",
                    reason: $"Bad PIN for {actionLabel}",
                    success: false)
                .ConfigureAwait(true);
            StatusMessage = auth.ErrorMessage ?? "PIN not recognised.";
            return false;
        }

        var role = (auth.Role ?? string.Empty).Trim();
        var isManager = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "treasurer", StringComparison.OrdinalIgnoreCase);
        if (!isManager)
        {
            await _audit
                .LogAsync(
                    AuditActions.PermissionDenied,
                    AuditEntityTypes.System,
                    entityId: "SquareRecovery",
                    reason: $"Non-manager PIN used for {actionLabel}",
                    success: false)
                .ConfigureAwait(true);
            StatusMessage = "PIN belongs to a staff account, not Admin/Treasurer.";
            return false;
        }

        return true;
    }

    private async Task<string?> PromptForReasonAsync(string title, string subtitle)
    {
        var xamlRoot = _windowHandle.GetXamlRoot();
        if (xamlRoot is null)
        {
            return null;
        }

        var box = new TextBox
        {
            PlaceholderText = "Required note / reason",
            AcceptsReturn = true,
            MinHeight = 90,
            TextWrapping = TextWrapping.Wrap,
        };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = subtitle,
            TextWrapping = TextWrapping.WrapWholeWords,
        });
        panel.Children.Add(box);

        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        PosContentDialogHelper.ApplyPosStyle(dlg);
        var result = await dlg.ShowAsync().AsTask().ConfigureAwait(true);
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(box.Text) ? null : box.Text.Trim();
    }

    private static string BuildDetailsJson(SquareRecoveryRowVm row, params (string key, string value)[] extras)
    {
        var doc = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["squarePaymentId"] = row.SquarePaymentIdText,
            ["squareCheckoutId"] = row.SquareCheckoutIdText,
            ["paymentType"] = row.PaymentTypeText,
            ["tabLegacyId"] = row.TabLegacyId,
            ["baseAmount"] = row.BaseAmount,
            ["surchargeAmount"] = row.SurchargeAmount,
            ["chargedAmount"] = row.ChargedAmount,
        };

        foreach (var (key, value) in extras)
        {
            doc[key] = value;
        }

        return JsonSerializer.Serialize(doc);
    }
}

public sealed class SquareRecoveryRowVm : ObservableViewModel
{
    private static readonly CultureInfo Cult = CultureInfo.CurrentCulture;

    private readonly SquareRecoveryRow _row;

    public SquareRecoveryRowVm(SquareRecoveryRow row, SquareRecoveryViewModel host)
    {
        _row = row;
        AttemptId = row.AttemptId;
        OccurredAtText = row.OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Cult);
        StatusText = string.IsNullOrWhiteSpace(row.Status) ? "—" : row.Status;
        PaymentTypeText = string.IsNullOrWhiteSpace(row.PaymentType) ? "—" : row.PaymentType;
        TabLegacyId = row.TabLegacyId;
        BaseAmount = row.BaseAmount;
        SurchargeAmount = row.SurchargeAmount;
        ChargedAmount = row.ChargedAmount;
        BaseAmountText = row.BaseAmount.ToString("C2", Cult);
        SurchargeAmountText = row.SurchargeAmount.ToString("C2", Cult);
        ChargedAmountText = row.ChargedAmount.ToString("C2", Cult);
        SquarePaymentIdText = row.SquarePaymentId ?? "—";
        SquareCheckoutIdText = row.SquareCheckoutId ?? "—";
        IdempotencyKeyText = row.IdempotencyKey ?? "—";
        OperatorText = string.IsNullOrWhiteSpace(row.InitiatedByStaffName) ? "—" : row.InitiatedByStaffName;
        RecoveryStatusText = string.IsNullOrWhiteSpace(row.RecoveryStatus) ? "Unresolved" : row.RecoveryStatus;
        NoteText = row.RecoveryNote;
        TabLegacyIdText = string.IsNullOrWhiteSpace(row.TabLegacyId) ? "—" : row.TabLegacyId;
        FailureReasonText = string.IsNullOrWhiteSpace(row.FailureReason) ? string.Empty : row.FailureReason;

        HasRecoverablePayload = row.HasRecoverablePayload;
        LinkPitstopCommand = new AsyncRelayCommand(() => host.LinkPitstopAsync(this));
        LinkTabCommand = new AsyncRelayCommand(() => host.LinkTabAsync(this));
        RecoverSaleCommand = new AsyncRelayCommand(() => host.RecoverPitstopSaleAsync(this), () => HasRecoverablePayload);
        IgnoreCommand = new AsyncRelayCommand(() => host.IgnoreRecoveryAsync(this));
        ReconcileCommand = new AsyncRelayCommand(() => host.MarkManuallyReconciledAsync(this));
        AddNoteCommand = new AsyncRelayCommand(() => host.AddNoteAsync(this));
    }

    public long AttemptId { get; }

    public string OccurredAtText { get; }

    public string StatusText { get; }

    public string PaymentTypeText { get; }

    public string? TabLegacyId { get; }

    public string TabLegacyIdText { get; }

    public decimal BaseAmount { get; }

    public decimal SurchargeAmount { get; }

    public decimal ChargedAmount { get; }

    public string BaseAmountText { get; }

    public string SurchargeAmountText { get; }

    public string ChargedAmountText { get; }

    public string SquarePaymentIdText { get; }

    public string SquareCheckoutIdText { get; }

    public string IdempotencyKeyText { get; }

    public string OperatorText { get; }

    public string RecoveryStatusText { get; }

    public string? NoteText { get; }

    public string FailureReasonText { get; }

    public bool HasRecoverablePayload { get; }

    public IAsyncRelayCommand LinkPitstopCommand { get; }

    public IAsyncRelayCommand LinkTabCommand { get; }

    public IAsyncRelayCommand RecoverSaleCommand { get; }

    public IAsyncRelayCommand IgnoreCommand { get; }

    public IAsyncRelayCommand ReconcileCommand { get; }

    public IAsyncRelayCommand AddNoteCommand { get; }
}
