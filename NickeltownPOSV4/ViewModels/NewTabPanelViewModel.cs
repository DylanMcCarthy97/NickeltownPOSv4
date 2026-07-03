using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class NewTabPanelViewModel : ObservableViewModel
{
    private readonly ITabManagementRepository _tabs;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly ITabWorkspaceUndoStack _undo;

    private readonly ISlidePanelService _slide;

    private readonly IInputOverlayService _inputOverlay;

    private string _tabName = string.Empty;

    private decimal _startingBalance;

    private string _notes = string.Empty;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    public NewTabPanelViewModel(
        ITabManagementRepository tabs,
        ITabWorkspaceRefreshBus refreshBus,
        ITabWorkspaceUndoStack undo,
        ISlidePanelService slide,
        IInputOverlayService inputOverlay)
    {
        _tabs = tabs;
        _refreshBus = refreshBus;
        _undo = undo;
        _slide = slide;
        _inputOverlay = inputOverlay;

        CancelCommand = new RelayCommand(ClosePanel);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        BeginNameEntryCommand = new AsyncRelayCommand(BeginNameEntryAsync, () => !IsBusy);
        BeginStartingFundsCommand = new AsyncRelayCommand(BeginStartingFundsAsync, () => !IsBusy);
        BeginNotesEntryCommand = new AsyncRelayCommand(BeginNotesEntryAsync, () => !IsBusy);
    }

    public IRelayCommand CancelCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand BeginNameEntryCommand { get; }

    public IAsyncRelayCommand BeginStartingFundsCommand { get; }

    public IAsyncRelayCommand BeginNotesEntryCommand { get; }

    public string TabName
    {
        get => _tabName;
        private set
        {
            if (SetProperty(ref _tabName, value))
            {
                StatusMessage = string.Empty;
                OnPropertyChanged(nameof(NameEntrySummary));
            }
        }
    }

    public string NameEntrySummary =>
        string.IsNullOrWhiteSpace(TabName) ? "Tap to enter member / account name" : TabName.Trim();

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
                SaveCommand.NotifyCanExecuteChanged();
                BeginNameEntryCommand.NotifyCanExecuteChanged();
                BeginStartingFundsCommand.NotifyCanExecuteChanged();
                BeginNotesEntryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private async Task BeginNameEntryAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var result = await _inputOverlay.ShowKeyboardAsync(TabName, "Member / account name", CancellationToken.None).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        TabName = result;
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

    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var label = TabName.Trim();
            if (string.IsNullOrEmpty(label))
            {
                StatusMessage = "Member / account name is required.";
                return;
            }

            var r = await _tabs
                .CreateTabAsync(label, PosTabAccountKind.Member, _startingBalance, null, Notes.Trim())
                .ConfigureAwait(true);
            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Could not create tab.";
                return;
            }

            _undo.PushUndo(
                "Undo new tab (soft-remove)",
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
            _inputOverlay.Close();
            _slide.Close();
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
