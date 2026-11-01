using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Payments;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Services.Tabs;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Shared tab card-fix overlay: check reader, save credit, retry or take cash. No Admin.</summary>
public sealed class TabCardFixSession : ObservableViewModel
{
    private readonly ISquareRecoveryRepository _rows;
    private readonly SquareRecoveryService _square;
    private readonly IPitstopPaymentRecoveryService _recovery;
    private readonly string _savedNoun;

    private long _attemptId;
    private decimal _amount;
    private bool _canRebuild;
    private bool _canAskReader = true;
    private PitstopCardFixStep _step = PitstopCardFixStep.AskCheck;
    private bool _showOverlay;
    private bool _isBusy;
    private PitstopCardFixCopy _copy = TabCardFixGuide.Build(PitstopCardFixStep.AskCheck, 0m, false);

    public TabCardFixSession(
        ISquareRecoveryRepository rows,
        SquareRecoveryService square,
        IPitstopPaymentRecoveryService recovery,
        string savedNoun = "credit")
    {
        _rows = rows;
        _square = square;
        _recovery = recovery;
        _savedNoun = savedNoun;
        CheckCommand = new AsyncRelayCommand(CheckAsync, () => CanRun && _copy.ShowCheck);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => CanRun && _copy.ShowSaveSale);
        MarkNotChargedCommand = new AsyncRelayCommand(MarkNotChargedAsync, () => CanRun && _copy.ShowNotCharged);
        DismissCommand = new AsyncRelayCommand(DismissAsync, () => ShowOverlay && _copy.ShowOk);
        RetryCardCommand = new RelayCommand(() => OnRetryCard?.Invoke(), () => CanRun && _copy.ShowRetryCard);
        TakeCashCommand = new RelayCommand(() => OnTakeCash?.Invoke(), () => CanRun && _copy.ShowTakeCash);
    }

    public Action? OnRetryCard { get; set; }

    public Action? OnTakeCash { get; set; }

    public Func<Task>? OnSavedAsync { get; set; }

    public IAsyncRelayCommand CheckCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand MarkNotChargedCommand { get; }

    public IAsyncRelayCommand DismissCommand { get; }

    public IRelayCommand RetryCardCommand { get; }

    public IRelayCommand TakeCashCommand { get; }

    public bool ShowOverlay
    {
        get => _showOverlay;
        private set
        {
            if (SetProperty(ref _showOverlay, value))
            {
                NotifyCopy();
            }
        }
    }

    public string Title => _copy.Title;

    public string Body => _copy.Body;

    public string AmountText => _copy.AmountText;

    public bool ShowCheck => _copy.ShowCheck;

    public bool ShowSave => _copy.ShowSaveSale;

    public bool ShowNotCharged => _copy.ShowNotCharged;

    public bool ShowOk => _copy.ShowOk;

    public bool ShowRetryCard => _copy.ShowRetryCard;

    public bool ShowTakeCash => _copy.ShowTakeCash;

    public string CheckLabel => _copy.CheckLabel;

    public string SaveLabel => _copy.SaveLabel;

    public string NotChargedLabel => _copy.NotChargedLabel;

    public string OkLabel => _copy.OkLabel;

    public string RetryCardLabel => _copy.RetryCardLabel;

    public string TakeCashLabel => _copy.TakeCashLabel;

    public decimal Amount => _amount;

    private bool CanRun => ShowOverlay && !_isBusy;

    public async Task OpenAsync(long attemptId, decimal amount, bool theyPaid, bool canRebuild)
    {
        _attemptId = attemptId;
        _amount = amount;
        _canRebuild = canRebuild;
        var hasCheckout = false;
        if (attemptId > 0)
        {
            var row = await FindAsync(attemptId).ConfigureAwait(true);
            hasCheckout = !string.IsNullOrWhiteSpace(row?.SquareCheckoutId);
            if (row is not null)
            {
                _canRebuild = row.HasRecoverablePayload || row.Status is "TerminalApproved" or "Approved" or "Completed" || canRebuild;
            }
        }

        _canAskReader = hasCheckout;
        var step = PitstopCardFixGuide.OpeningStep(theyPaid, hasCheckout);
        ShowOverlay = true;
        if (PitstopCardFixGuide.ShouldAutoCheck(step))
        {
            Apply(PitstopCardFixStep.Checking);
            await CheckAsync().ConfigureAwait(true);
            return;
        }

        Apply(step);
        if (PitstopCardFixGuide.ShouldAutoSave(step, _canRebuild))
        {
            await TryAutoSaveAsync().ConfigureAwait(true);
        }
    }

    public void OpenNotCharged(decimal amount)
    {
        _attemptId = 0;
        _amount = amount;
        _canRebuild = false;
        _canAskReader = false;
        Apply(PitstopCardFixStep.NotCharged);
        ShowOverlay = true;
    }

    public async Task OpenLeftoverForTabAsync(string? tabLegacyId)
    {
        if (ShowOverlay || string.IsNullOrWhiteSpace(tabLegacyId))
        {
            return;
        }

        var row = await FindOpenTabAsync(tabLegacyId).ConfigureAwait(true);
        if (row is null)
        {
            return;
        }

        var paid = row.Status is "TerminalApproved" or "Approved" or "Completed";
        await OpenAsync(row.AttemptId, row.ChargedAmount, paid, paid).ConfigureAwait(true);
    }

    public void Hide()
    {
        ShowOverlay = false;
    }

    private async Task CheckAsync()
    {
        if (_attemptId <= 0)
        {
            Apply(PitstopCardFixStep.Unknown);
            return;
        }

        _isBusy = true;
        NotifyCommands();
        try
        {
            var check = await _square.RefreshStatusAsync(_attemptId).ConfigureAwait(true);
            var row = await FindAsync(_attemptId).ConfigureAwait(true);
            if (row is null)
            {
                Apply(PitstopCardFixStep.NotCharged);
                return;
            }

            _amount = row.ChargedAmount;
            _canRebuild = row.Status is "TerminalApproved" or "Approved" or "Completed";
            _canAskReader = !string.IsNullOrWhiteSpace(row.SquareCheckoutId);
            var approved = _canRebuild;
            var confirmedNotPaid = check.Ok && !approved;
            var step = PitstopCardFixGuide.AfterSquareCheck(approved, confirmedNotPaid, _canRebuild);
            if (!check.Ok && !approved)
            {
                step = PitstopCardFixStep.Unknown;
            }

            Apply(step);
        }
        catch
        {
            Apply(PitstopCardFixStep.Unknown);
        }
        finally
        {
            _isBusy = false;
            NotifyCommands();
        }

        if (PitstopCardFixGuide.ShouldAutoSave(_step, _canRebuild))
        {
            await TryAutoSaveAsync().ConfigureAwait(true);
        }
    }

    private Task TryAutoSaveAsync()
    {
        Apply(PitstopCardFixStep.Saving);
        return SaveAsync();
    }

    private async Task SaveAsync()
    {
        if (_attemptId <= 0)
        {
            return;
        }

        _isBusy = true;
        NotifyCommands();
        try
        {
            var result = await _square.SaveTabTopUpAsync(_attemptId).ConfigureAwait(true);
            if (!result.Ok)
            {
                _copy = new PitstopCardFixCopy
                {
                    Title = "Could not save the credit",
                    Body = PosUserMessage.OrFallback(result.ErrorMessage, "We couldn't save that credit. Try Find out if they paid again."),
                    AmountText = _amount.ToString("C2", CultureInfo.CurrentCulture),
                    ShowCheck = true,
                    ShowSaveSale = _canRebuild,
                    SaveLabel = "Save the credit",
                };
                NotifyCopy();
                return;
            }

            if (OnSavedAsync is not null)
            {
                await OnSavedAsync().ConfigureAwait(true);
            }

            Apply(PitstopCardFixStep.Saved);
        }
        finally
        {
            _isBusy = false;
            NotifyCommands();
        }
    }

    private async Task MarkNotChargedAsync()
    {
        if (_attemptId > 0)
        {
            _isBusy = true;
            NotifyCommands();
            try
            {
                var result = await _recovery
                    .IgnoreAsync(_attemptId, "Staff said they were not charged (tab till)")
                    .ConfigureAwait(true);
                if (!result.Ok)
                {
                    _copy = new PitstopCardFixCopy
                    {
                        Title = "Could not clear this card",
                        Body = PosUserMessage.OrFallback(result.ErrorMessage, "We couldn't clear this one. Try Find out if they paid."),
                        AmountText = _amount.ToString("C2", CultureInfo.CurrentCulture),
                        ShowCheck = true,
                        ShowNotCharged = true,
                    };
                    NotifyCopy();
                    return;
                }
            }
            finally
            {
                _isBusy = false;
            }
        }

        Apply(PitstopCardFixStep.NotCharged);
        NotifyCommands();
    }

    private async Task DismissAsync()
    {
        if (PitstopCardFixGuide.WrittenDownClearsBanner(_step, _canRebuild) && _attemptId > 0)
        {
            await _rows
                .MarkManuallyReconciledAsync(_attemptId, "Staff wrote down the amount (tab till)")
                .ConfigureAwait(true);
        }

        ShowOverlay = false;
    }

    private void Apply(PitstopCardFixStep step)
    {
        _step = step;
        _copy = TabCardFixGuide.Build(step, _amount, _canRebuild, _canAskReader, _savedNoun);
        NotifyCopy();
    }

    private void NotifyCopy()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Body));
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(ShowCheck));
        OnPropertyChanged(nameof(ShowSave));
        OnPropertyChanged(nameof(ShowNotCharged));
        OnPropertyChanged(nameof(ShowOk));
        OnPropertyChanged(nameof(ShowRetryCard));
        OnPropertyChanged(nameof(ShowTakeCash));
        OnPropertyChanged(nameof(CheckLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(NotChargedLabel));
        OnPropertyChanged(nameof(OkLabel));
        OnPropertyChanged(nameof(RetryCardLabel));
        OnPropertyChanged(nameof(TakeCashLabel));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        CheckCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        MarkNotChargedCommand.NotifyCanExecuteChanged();
        DismissCommand.NotifyCanExecuteChanged();
        RetryCardCommand.NotifyCanExecuteChanged();
        TakeCashCommand.NotifyCanExecuteChanged();
    }

    private async Task<SquareRecoveryRow?> FindAsync(long attemptId)
    {
        var rows = await _rows.GetOrphanAttemptsAsync().ConfigureAwait(true);
        return rows.FirstOrDefault(r => r.AttemptId == attemptId);
    }

    private async Task<SquareRecoveryRow?> FindOpenTabAsync(string tabLegacyId)
    {
        var rows = await _rows.GetOrphanAttemptsAsync().ConfigureAwait(true);
        return rows.FirstOrDefault(r =>
            string.Equals(r.PaymentType, nameof(SquarePaymentAttemptType.TabTopUp), StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.TabLegacyId, tabLegacyId, StringComparison.Ordinal));
    }
}
