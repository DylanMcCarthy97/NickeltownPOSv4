using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class GuestCloseoutLineViewModel : ObservableViewModel
{
    private bool _isSelected = true;

    public GuestCloseoutLineViewModel(GuestCloseoutRow row, GuestCloseoutPanelViewModel host)
    {
        _host = host;
        LegacyId = row.LegacyId;
        DisplayName = row.DisplayName;
        Balance = row.Balance;
        BalanceText = FormatMoney(row.Balance);
        LastActivityText = row.LastActivityText;
        CreatedText = row.CreatedText;
        RecordPaymentCommand = new AsyncRelayCommand(RecordPaymentAsync, () => Balance != 0m);
        OpenDrinksCommand = new RelayCommand(() => _host.OpenDrinksForLine(this));
        SelectOnBoardCommand = new RelayCommand(() => _host.SelectLineOnBoard(this));
    }

    private readonly GuestCloseoutPanelViewModel _host;

    public IAsyncRelayCommand RecordPaymentCommand { get; }

    public IRelayCommand OpenDrinksCommand { get; }

    public IRelayCommand SelectOnBoardCommand { get; }

    private async Task RecordPaymentAsync() => await _host.RecordPaymentForLineAsync(this).ConfigureAwait(true);

    public string ActivityLine => $"Last {LastActivityText}{Environment.NewLine}Opened {CreatedText}";

    public string LegacyId { get; }

    public string DisplayName { get; }

    public decimal Balance { get; }

    public string BalanceText { get; }

    public string LastActivityText { get; }

    public string CreatedText { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private static string FormatMoney(decimal value)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var abs = Math.Abs(value).ToString("0.00", inv);
        return value < 0m ? "-$" + abs : "$" + abs;
    }
}

public sealed class GuestCloseoutPanelViewModel : ObservableViewModel
{
    public const string DefaultCloseReason = "End of night guest closeout";

    private readonly ITabManagementRepository _tabs;

    private readonly ITabFundsService _funds;

    private readonly IInputOverlayService _inputOverlay;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly ISlidePanelService _slide;

    private readonly IUserSessionService _session;

    private readonly ITabWorkspaceUndoStack _undo;

    private readonly TabsWorkspaceViewModel _board;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    public GuestCloseoutPanelViewModel(
        ITabManagementRepository tabs,
        ITabFundsService funds,
        IInputOverlayService inputOverlay,
        ITabWorkspaceRefreshBus refreshBus,
        ISlidePanelService slide,
        IUserSessionService session,
        ITabWorkspaceUndoStack undo,
        TabsWorkspaceViewModel board)
    {
        _tabs = tabs;
        _funds = funds;
        _inputOverlay = inputOverlay;
        _refreshBus = refreshBus;
        _slide = slide;
        _session = session;
        _undo = undo;
        _board = board;

        Lines = new ObservableCollection<GuestCloseoutLineViewModel>();
        CloseCommand = new RelayCommand(() => _slide.Close());
        RefreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        SelectAllCommand = new RelayCommand(SelectAll, () => Lines.Count > 0 && !IsBusy);
        SelectNoneCommand = new RelayCommand(SelectNone, () => Lines.Count > 0 && !IsBusy);
        CloseSelectedCommand = new AsyncRelayCommand(CloseSelectedAsync, () => !IsBusy && HasSelection);
        CloseZeroCommand = new AsyncRelayCommand(CloseZeroAsync, () => !IsBusy);
        ArchiveSelectedCommand = new AsyncRelayCommand(ArchiveSelectedAsync, () => !IsBusy && HasSelection && _session.IsAdmin);
        ArchiveAllCommand = new AsyncRelayCommand(ArchiveAllAsync, () => !IsBusy && _session.IsAdmin);

        _session.PropertyChanged += (_, e) =>
        {
            if (e?.PropertyName is nameof(IUserSessionService.IsAdmin))
            {
                OnPropertyChanged(nameof(CanAdminArchive));
                ArchiveSelectedCommand.NotifyCanExecuteChanged();
                ArchiveAllCommand.NotifyCanExecuteChanged();
            }
        };

        _ = LoadAsync();
    }

    public bool CanAdminArchive => _session.IsAdmin;

    public ObservableCollection<GuestCloseoutLineViewModel> Lines { get; }

    public IRelayCommand CloseCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand SelectAllCommand { get; }

    public IRelayCommand SelectNoneCommand { get; }

    public IAsyncRelayCommand CloseSelectedCommand { get; }

    public IAsyncRelayCommand CloseZeroCommand { get; }

    public IAsyncRelayCommand ArchiveSelectedCommand { get; }

    public IAsyncRelayCommand ArchiveAllCommand { get; }

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
                NotifyWorkCommands();
            }
        }
    }

    private bool HasSelection => Lines.Any(l => l.IsSelected);

    private void NotifyWorkCommands()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        SelectNoneCommand.NotifyCanExecuteChanged();
        CloseSelectedCommand.NotifyCanExecuteChanged();
        CloseZeroCommand.NotifyCanExecuteChanged();
        ArchiveSelectedCommand.NotifyCanExecuteChanged();
        ArchiveAllCommand.NotifyCanExecuteChanged();
    }

    private void SelectAll()
    {
        foreach (var l in Lines)
        {
            l.IsSelected = true;
        }

        CloseSelectedCommand.NotifyCanExecuteChanged();
        ArchiveSelectedCommand.NotifyCanExecuteChanged();
    }

    private void SelectNone()
    {
        foreach (var l in Lines)
        {
            l.IsSelected = false;
        }

        CloseSelectedCommand.NotifyCanExecuteChanged();
        ArchiveSelectedCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var rows = await _tabs.GetOpenGuestTabsForCloseoutAsync(CancellationToken.None).ConfigureAwait(true);
            Lines.Clear();
            foreach (var r in rows)
            {
                var vm = new GuestCloseoutLineViewModel(r, this);
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(GuestCloseoutLineViewModel.IsSelected))
                    {
                        CloseSelectedCommand.NotifyCanExecuteChanged();
                        ArchiveSelectedCommand.NotifyCanExecuteChanged();
                    }
                };
                Lines.Add(vm);
            }

            if (Lines.Count == 0)
            {
                StatusMessage = "No open guest tabs.";
            }

            NotifyWorkCommands();
        }
        catch
        {
            StatusMessage = "Could not load guest tabs.";
            NotifyWorkCommands();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CloseSelectedAsync()
    {
        var ids = Lines.Where(l => l.IsSelected).Select(l => l.LegacyId).ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var r = await _tabs.CloseGuestTabsEndOfNightAsync(ids, _session.ActiveStaffId, DefaultCloseReason, CancellationToken.None)
                .ConfigureAwait(true);
            StatusMessage = r.Ok ? "Selected guest tabs were closed and archived." : (r.ErrorMessage ?? "Close failed.");
            if (r.Ok)
            {
                _refreshBus.RequestRefresh();
                await LoadAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CloseZeroAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var r = await _tabs.CloseAllZeroBalanceGuestTabsAsync(_session.ActiveStaffId, DefaultCloseReason, CancellationToken.None)
                .ConfigureAwait(true);
            StatusMessage = r.Ok ? "Zero-balance guest tabs were closed and archived." : (r.ErrorMessage ?? "No zero-balance guests to close.");
            if (r.Ok)
            {
                _refreshBus.RequestRefresh();
                await LoadAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ArchiveSelectedAsync()
    {
        if (!_session.IsAdmin)
        {
            StatusMessage = "Archiving tabs is restricted to admins.";
            return;
        }

        var ids = Lines.Where(l => l.IsSelected).Select(l => l.LegacyId).ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var r = await _tabs.ArchiveGuestTabsAsync(ids, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = r.Ok ? "Selected guest tabs were archived." : (r.ErrorMessage ?? "Archive failed.");
            if (r.Ok)
            {
                _refreshBus.RequestRefresh();
                await LoadAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ArchiveAllAsync()
    {
        if (!_session.IsAdmin)
        {
            StatusMessage = "Archiving tabs is restricted to admins.";
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var r = await _tabs.ArchiveAllOpenGuestTabsAsync(CancellationToken.None).ConfigureAwait(true);
            StatusMessage = r.Ok ? "All open guest tabs were archived." : (r.ErrorMessage ?? "Archive failed.");
            if (r.Ok)
            {
                _refreshBus.RequestRefresh();
                await LoadAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal void SelectLineOnBoard(GuestCloseoutLineViewModel line)
    {
        _board.SelectOpenTabById(line.LegacyId);
        StatusMessage = $"Selected {line.DisplayName} — use Drinks on the board or the row Drinks button.";
    }

    internal void OpenDrinksForLine(GuestCloseoutLineViewModel line)
    {
        _inputOverlay.Close();
        _slide.Close();
        _board.OpenDrinksForTab(line.LegacyId);
    }

    /// <summary>Record cash collected (negative balance) or draw down tab credit (positive balance) during closeout.</summary>
    internal async Task RecordPaymentForLineAsync(GuestCloseoutLineViewModel line)
    {
        if (IsBusy || line.Balance == 0m)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var defaultAmount = Math.Abs(line.Balance);
            var title = line.Balance < 0m
                ? $"Cash in — {line.DisplayName}"
                : $"Settle credit — {line.DisplayName}";

            var entered = await _inputOverlay
                .ShowNumpadAsync(defaultAmount, title, allowSignedAmount: false, CancellationToken.None)
                .ConfigureAwait(true);
            if (entered is null)
            {
                return;
            }

            var amt = decimal.Round(entered.Value, 2, System.MidpointRounding.AwayFromZero);
            if (amt <= 0m)
            {
                StatusMessage = "Enter a positive amount.";
                return;
            }

            TabFundsCommitResult r;
            if (line.Balance < 0m)
            {
                r = await _funds
                    .CommitFundMovementAsync(
                        line.LegacyId,
                        "cash",
                        amt,
                        "Guest closeout — cash payment",
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }
            else
            {
                var draw = decimal.Min(amt, line.Balance);
                r = await _funds
                    .CommitFundMovementAsync(
                        line.LegacyId,
                        "manual",
                        -draw,
                        "Guest closeout — settle tab credit",
                        CancellationToken.None)
                    .ConfigureAwait(true);
            }

            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Could not record payment.";
                return;
            }

            var tabLegacy = line.LegacyId;
            var batchId = r.FundCommitBatchId;

            StatusMessage = "Payment recorded — balances updated.";
            _refreshBus.RequestRefresh();
            await LoadAsync().ConfigureAwait(true);

            if (!string.IsNullOrEmpty(batchId))
            {
                _undo.PushUndo(
                    $"Undo last guest closeout ({line.DisplayName})",
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
                        await LoadAsync().ConfigureAwait(true);
                        return true;
                    });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
