using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class GuestTabPanelViewModel : ObservableViewModel
{
    private readonly ITabManagementRepository _tabs;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly ITabWorkspaceUndoStack _undo;

    private readonly ISlidePanelService _slide;

    private readonly IInputOverlayService _inputOverlay;

    private readonly TabsWorkspaceViewModel _board;

    private string _guestName = string.Empty;

    private decimal _startingBalance;

    private string _notes = string.Empty;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    public GuestTabPanelViewModel(
        ITabManagementRepository tabs,
        ITabWorkspaceRefreshBus refreshBus,
        ITabWorkspaceUndoStack undo,
        ISlidePanelService slide,
        IInputOverlayService inputOverlay,
        TabsWorkspaceViewModel board)
    {
        _tabs = tabs;
        _refreshBus = refreshBus;
        _undo = undo;
        _slide = slide;
        _inputOverlay = inputOverlay;
        _board = board;

        CancelCommand = new RelayCommand(ClosePanel);
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => !IsBusy);
        BeginNameEntryCommand = new AsyncRelayCommand(BeginNameEntryAsync, () => !IsBusy);
        BeginStartingFundsCommand = new AsyncRelayCommand(BeginStartingFundsAsync, () => !IsBusy);
        BeginNotesEntryCommand = new AsyncRelayCommand(BeginNotesEntryAsync, () => !IsBusy);

        _ = UseSuggestedGuestNameAsync();
    }

    public IRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand CreateCommand { get; }

    public IAsyncRelayCommand BeginNameEntryCommand { get; }

    public IAsyncRelayCommand BeginStartingFundsCommand { get; }

    public IAsyncRelayCommand BeginNotesEntryCommand { get; }

    public string GuestName
    {
        get => _guestName;
        private set
        {
            if (SetProperty(ref _guestName, value))
            {
                StatusMessage = string.Empty;
                OnPropertyChanged(nameof(NameEntrySummary));
            }
        }
    }

    public string NameEntrySummary =>
        string.IsNullOrWhiteSpace(GuestName) ? "Tap to enter guest name (required)" : GuestName.Trim();

    public string StartingFundsDisplay =>
        _startingBalance == 0m
            ? "Tap to set optional starting funds"
            : FormatMoney(_startingBalance);

    public string NotesSummary =>
        string.IsNullOrWhiteSpace(_notes) ? "Tap to add optional notes" : _notes.Trim();

    public string Notes
    {
        get => _notes;
        private set
        {
            if (SetProperty(ref _notes, value))
            {
                OnPropertyChanged(nameof(NotesSummary));
            }
        }
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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CreateCommand.NotifyCanExecuteChanged();
                BeginNameEntryCommand.NotifyCanExecuteChanged();
                BeginStartingFundsCommand.NotifyCanExecuteChanged();
                BeginNotesEntryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private async Task UseSuggestedGuestNameAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            GuestName = await _tabs.SuggestNextGuestSequenceNameAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
            GuestName = "Guest 1";
        }
    }

    private async Task BeginNameEntryAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var result = await _inputOverlay.ShowKeyboardAsync(GuestName, "Guest name", CancellationToken.None).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        GuestName = result;
    }

    private async Task BeginStartingFundsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var result = await _inputOverlay.ShowNumpadAsync(_startingBalance, "Starting funds (optional)", false, CancellationToken.None)
            .ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        _startingBalance = decimal.Round(result.Value, 2, MidpointRounding.AwayFromZero);
        OnPropertyChanged(nameof(StartingFundsDisplay));
    }

    private async Task BeginNotesEntryAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var result = await _inputOverlay.ShowKeyboardAsync(Notes, "Notes (optional)", CancellationToken.None).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        Notes = result;
    }

    private void ClosePanel()
    {
        _inputOverlay.Close();
        _slide.Close();
    }

    private async Task CreateAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var label = GuestName.Trim();
            if (string.IsNullOrEmpty(label))
            {
                StatusMessage = "Guest name is required.";
                return;
            }

            var r = await _tabs.CreateTabAsync(label, PosTabAccountKind.Guest, _startingBalance, null, Notes.Trim())
                .ConfigureAwait(true);
            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Could not create guest tab.";
                return;
            }

            _undo.PushUndo(
                "Undo guest tab (soft-remove)",
                async () =>
                {
                    if (string.IsNullOrEmpty(r.CreatedLegacyId))
                    {
                        return false;
                    }

                    var del = await _tabs.SoftDeleteTabAsync(r.CreatedLegacyId!).ConfigureAwait(true);
                    if (!del.Ok)
                    {
                        return false;
                    }

                    _refreshBus.RequestRefresh();
                    return true;
                });

            _refreshBus.RequestRefresh();
            await _board.RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
            if (!string.IsNullOrEmpty(r.CreatedLegacyId))
            {
                _board.SelectOpenTabById(r.CreatedLegacyId);
            }

            ClosePanel();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatMoney(decimal value)
    {
        var inv = CultureInfo.InvariantCulture;
        var abs = Math.Abs(value).ToString("0.00", inv);
        return value < 0m ? "-$" + abs : "$" + abs;
    }
}
