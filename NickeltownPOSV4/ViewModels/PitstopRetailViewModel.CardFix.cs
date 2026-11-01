using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;

namespace NickeltownPOSV4.ViewModels;

public sealed partial class PitstopRetailViewModel
{
    private long _cardFixAttemptId;
    private decimal _cardFixAmount;
    private bool _cardFixCanRebuildSale;
    private bool _showCardFixOverlay;
    private bool _showCardFixBanner;
    private string _cardFixBannerText = string.Empty;
    private PitstopCardFixCopy _cardFixCopy = PitstopCardFixGuide.Build(PitstopCardFixStep.AskCheck, 0m, false);

    public IAsyncRelayCommand CheckCardFixCommand { get; private set; } = null!;

    public IAsyncRelayCommand SaveCardFixSaleCommand { get; private set; } = null!;

    public IAsyncRelayCommand MarkCardNotChargedCommand { get; private set; } = null!;

    public IRelayCommand DismissCardFixCommand { get; private set; } = null!;

    public IAsyncRelayCommand OpenCardFixFromBannerCommand { get; private set; } = null!;

    public bool ShowCardFixOverlay
    {
        get => _showCardFixOverlay;
        private set
        {
            if (SetProperty(ref _showCardFixOverlay, value))
            {
                OnPropertyChanged(nameof(IsPaymentLocked));
                OnPropertyChanged(nameof(ShowCardFixBanner));
                NotifyCardFixCommands();
            }
        }
    }

    public bool ShowCardFixBanner => _showCardFixBanner && !ShowCardFixOverlay;

    public string CardFixBannerText => _cardFixBannerText;

    public string CardFixTitle => _cardFixCopy.Title;

    public string CardFixBody => _cardFixCopy.Body;

    public string CardFixAmountText => _cardFixCopy.AmountText;

    public bool CardFixShowCheck => _cardFixCopy.ShowCheck;

    public bool CardFixShowSaveSale => _cardFixCopy.ShowSaveSale;

    public bool CardFixShowNotCharged => _cardFixCopy.ShowNotCharged;

    public bool CardFixShowOk => _cardFixCopy.ShowOk;

    public string CardFixCheckLabel => _cardFixCopy.CheckLabel;

    public string CardFixSaveLabel => _cardFixCopy.SaveLabel;

    public string CardFixNotChargedLabel => _cardFixCopy.NotChargedLabel;

    public string CardFixOkLabel => _cardFixCopy.OkLabel;

    private bool CanRunCardFix => ShowCardFixOverlay && !IsBusy && !IsSendingSquare;

    internal async Task RefreshCardFixBannerAsync()
    {
        var row = await FindOpenPitstopCardAsync().ConfigureAwait(true);
        _showCardFixBanner = row is not null;
        _cardFixBannerText = row is null
            ? string.Empty
            : PitstopCardFixGuide.BannerText(row.ChargedAmount);
        OnPropertyChanged(nameof(ShowCardFixBanner));
        OnPropertyChanged(nameof(CardFixBannerText));
        OpenCardFixFromBannerCommand.NotifyCanExecuteChanged();
    }

    internal async Task OpenCardFixAsync(long attemptId, decimal amount, bool theyPaid, bool canRebuildSale)
    {
        _cardFixAttemptId = attemptId;
        _cardFixAmount = amount;
        _cardFixCanRebuildSale = canRebuildSale;
        var hasCheckoutId = false;
        if (attemptId > 0)
        {
            var existing = await FindPitstopCardAsync(attemptId).ConfigureAwait(true);
            hasCheckoutId = !string.IsNullOrWhiteSpace(existing?.SquareCheckoutId);
        }

        var step = PitstopCardFixGuide.OpeningStep(theyPaid, hasCheckoutId);

        ApplyCardFixCopy(step);
        ShowPaymentProgressOverlay = false;
        ShowCardFixOverlay = true;
        await RefreshCardFixBannerAsync().ConfigureAwait(true);
    }

    internal void OpenCardFixNotCharged(decimal amount)
    {
        _cardFixAttemptId = 0;
        _cardFixAmount = amount;
        _cardFixCanRebuildSale = false;
        ApplyCardFixCopy(PitstopCardFixStep.NotCharged);
        ShowPaymentProgressOverlay = false;
        ShowCardFixOverlay = true;
    }

    private async Task OpenCardFixFromBannerAsync()
    {
        var row = await FindOpenPitstopCardAsync().ConfigureAwait(true);
        if (row is null)
        {
            await RefreshCardFixBannerAsync().ConfigureAwait(true);
            return;
        }

        var paid = IsApprovedStatus(row.Status);
        await OpenCardFixAsync(row.AttemptId, row.ChargedAmount, paid, row.HasRecoverablePayload).ConfigureAwait(true);
    }

    private async Task CheckCardFixAsync()
    {
        if (_cardFixAttemptId <= 0)
        {
            ApplyCardFixCopy(PitstopCardFixStep.Unknown);
            return;
        }

        IsBusy = true;
        try
        {
            var check = await _squareRecovery.RefreshStatusAsync(_cardFixAttemptId).ConfigureAwait(true);
            var row = await FindPitstopCardAsync(_cardFixAttemptId).ConfigureAwait(true);
            if (row is null)
            {
                ApplyCardFixCopy(PitstopCardFixStep.NotCharged);
                return;
            }

            _cardFixAmount = row.ChargedAmount;
            _cardFixCanRebuildSale = row.HasRecoverablePayload;
            var approved = IsApprovedStatus(row.Status);
            var confirmedNotPaid = check.Ok && !approved;
            ApplyCardFixCopy(PitstopCardFixGuide.AfterSquareCheck(approved, confirmedNotPaid, row.HasRecoverablePayload));
            if (!check.Ok && !approved)
            {
                ApplyCardFixCopy(PitstopCardFixStep.Unknown);
            }
        }
        catch
        {
            ApplyCardFixCopy(PitstopCardFixStep.Unknown);
        }
        finally
        {
            IsBusy = false;
            NotifyCardFixCommands();
        }
    }

    private async Task SaveCardFixSaleAsync()
    {
        if (_cardFixAttemptId <= 0)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _pitstopRecovery.RecoverPitstopSaleAsync(_cardFixAttemptId).ConfigureAwait(true);
            if (!result.Ok)
            {
                _cardFixCopy = new PitstopCardFixCopy
                {
                    Title = "Could not save the sale",
                    Body = PosUserMessage.OrFallback(result.ErrorMessage, "We couldn't save that sale. Try Find out if they paid again."),
                    AmountText = _cardFixAmount.ToString("C2", CultureInfo.CurrentCulture),
                    ShowCheck = true,
                    ShowSaveSale = _cardFixCanRebuildSale,
                };
                RaiseCardFixCopy();
                return;
            }

            ClearCart();
            ApplyCardFixCopy(PitstopCardFixStep.Saved);
            StatusMessage = "Sale saved. Do not charge them again.";
        }
        finally
        {
            IsBusy = false;
            NotifyCardFixCommands();
        }
    }

    private async Task MarkCardNotChargedAsync()
    {
        if (_cardFixAttemptId > 0)
        {
            IsBusy = true;
            try
            {
                var result = await _pitstopRecovery
                    .IgnoreAsync(_cardFixAttemptId, "Staff said they were not charged (Pitstop till)")
                    .ConfigureAwait(true);
                if (!result.Ok)
                {
                    _cardFixCopy = new PitstopCardFixCopy
                    {
                        Title = "Could not clear this card",
                        Body = PosUserMessage.OrFallback(result.ErrorMessage, "We couldn't clear this one. Try Find out if they paid."),
                        AmountText = _cardFixAmount.ToString("C2", CultureInfo.CurrentCulture),
                        ShowCheck = true,
                        ShowNotCharged = true,
                    };
                    RaiseCardFixCopy();
                    return;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        ApplyCardFixCopy(PitstopCardFixStep.NotCharged);
        StatusMessage = "They weren't charged. Take cash or start a new sale.";
        NotifyCardFixCommands();
    }

    private void DismissCardFix()
    {
        ShowCardFixOverlay = false;
        _ = RefreshCardFixBannerAsync();
    }

    private void ApplyCardFixCopy(PitstopCardFixStep step)
    {
        _cardFixCopy = PitstopCardFixGuide.Build(step, _cardFixAmount, _cardFixCanRebuildSale);
        RaiseCardFixCopy();
    }

    private void RaiseCardFixCopy()
    {
        OnPropertyChanged(nameof(CardFixTitle));
        OnPropertyChanged(nameof(CardFixBody));
        OnPropertyChanged(nameof(CardFixAmountText));
        OnPropertyChanged(nameof(CardFixShowCheck));
        OnPropertyChanged(nameof(CardFixShowSaveSale));
        OnPropertyChanged(nameof(CardFixShowNotCharged));
        OnPropertyChanged(nameof(CardFixShowOk));
        OnPropertyChanged(nameof(CardFixCheckLabel));
        OnPropertyChanged(nameof(CardFixSaveLabel));
        OnPropertyChanged(nameof(CardFixNotChargedLabel));
        OnPropertyChanged(nameof(CardFixOkLabel));
        NotifyCardFixCommands();
    }

    private void NotifyCardFixCommands()
    {
        CheckCardFixCommand?.NotifyCanExecuteChanged();
        SaveCardFixSaleCommand?.NotifyCanExecuteChanged();
        MarkCardNotChargedCommand?.NotifyCanExecuteChanged();
        DismissCardFixCommand?.NotifyCanExecuteChanged();
        OpenCardFixFromBannerCommand?.NotifyCanExecuteChanged();
    }

    private async Task<SquareRecoveryRow?> FindOpenPitstopCardAsync()
    {
        var rows = await _squareRecoveryRows.GetOrphanAttemptsAsync().ConfigureAwait(true);
        return rows.FirstOrDefault(r =>
            string.Equals(r.PaymentType, nameof(SquarePaymentAttemptType.PitstopSale), System.StringComparison.OrdinalIgnoreCase));
    }

    private async Task<SquareRecoveryRow?> FindPitstopCardAsync(long attemptId)
    {
        var rows = await _squareRecoveryRows.GetOrphanAttemptsAsync().ConfigureAwait(true);
        return rows.FirstOrDefault(r => r.AttemptId == attemptId);
    }

    private static bool IsApprovedStatus(string? status) =>
        status is "TerminalApproved" or "Approved" or "Completed";
}
