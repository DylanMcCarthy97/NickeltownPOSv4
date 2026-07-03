using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Tabs;

namespace NickeltownPOSV4.ViewModels;

public sealed class EditTabPanelViewModel : ObservableViewModel
{
    private readonly ITabManagementRepository _tabs;

    private readonly IEditTabSession _edit;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly ISlidePanelService _slide;

    private readonly IInputOverlayService _inputOverlay;

    private readonly ITabWorkspaceUndoStack _undo;

    private readonly IEditTabPanelHost _editHost;

    private string _legacyId = string.Empty;

    private PosTabAccountKind _loadedKind = PosTabAccountKind.Member;

    private string _tabName = string.Empty;

    private string _memberId = string.Empty;

    private string _notes = string.Empty;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    private bool _loadFailed;

    public EditTabPanelViewModel(
        ITabManagementRepository tabs,
        IEditTabSession edit,
        ITabWorkspaceRefreshBus refreshBus,
        ISlidePanelService slide,
        IInputOverlayService inputOverlay,
        ITabWorkspaceUndoStack undo,
        IEditTabPanelHost editHost)
    {
        _tabs = tabs;
        _edit = edit;
        _refreshBus = refreshBus;
        _slide = slide;
        _inputOverlay = inputOverlay;
        _undo = undo;
        _editHost = editHost;

        CancelCommand = new RelayCommand(ClosePanel);
        DeleteTabCommand = new RelayCommand(RequestDeleteTab, () => _editHost.CanDeleteCurrentTab && !IsBusy && !_loadFailed);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && !_loadFailed && !string.IsNullOrWhiteSpace(_legacyId));
        BeginNameEntryCommand = new AsyncRelayCommand(BeginNameEntryAsync, () => !IsBusy && !_loadFailed);
        BeginMemberIdEntryCommand = new AsyncRelayCommand(BeginMemberIdEntryAsync, () => !IsBusy && !_loadFailed && IsMemberTab);
        BeginNotesEntryCommand = new AsyncRelayCommand(BeginNotesEntryAsync, () => !IsBusy && !_loadFailed);
    }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand DeleteTabCommand { get; }

    public bool ShowDeleteTab => _editHost.CanDeleteCurrentTab;

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand BeginNameEntryCommand { get; }

    public IAsyncRelayCommand BeginMemberIdEntryCommand { get; }

    public IAsyncRelayCommand BeginNotesEntryCommand { get; }

    public bool IsMemberTab => _loadedKind == PosTabAccountKind.Member;

    public string NameEntrySummary =>
        string.IsNullOrWhiteSpace(_tabName) ? "Tap to enter tab name" : _tabName.Trim();

    public string MemberIdSummary =>
        string.IsNullOrWhiteSpace(_memberId) ? "Tap to enter optional member link id" : _memberId.Trim();

    public string NotesSummary =>
        string.IsNullOrWhiteSpace(_notes) ? "Tap to edit notes" : _notes.Trim();

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

    public string MemberId
    {
        get => _memberId;
        private set
        {
            if (SetProperty(ref _memberId, value))
            {
                OnPropertyChanged(nameof(MemberIdSummary));
            }
        }
    }

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
                BeginMemberIdEntryCommand.NotifyCanExecuteChanged();
                BeginNotesEntryCommand.NotifyCanExecuteChanged();
                DeleteTabCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public async Task LoadAsync()
    {
        _loadFailed = false;
        StatusMessage = string.Empty;
        SaveCommand.NotifyCanExecuteChanged();
        BeginNameEntryCommand.NotifyCanExecuteChanged();
        BeginMemberIdEntryCommand.NotifyCanExecuteChanged();
        BeginNotesEntryCommand.NotifyCanExecuteChanged();
        DeleteTabCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowDeleteTab));

        var id = (_edit.TabLegacyId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(id))
        {
            _loadFailed = true;
            StatusMessage = "No tab selected.";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        _legacyId = id;
        var row = await _tabs.GetTabEditorRowAsync(id).ConfigureAwait(true);
        if (row is null)
        {
            _loadFailed = true;
            StatusMessage = "Tab was not found.";
            SaveCommand.NotifyCanExecuteChanged();
            return;
        }

        TabName = row.DisplayName;
        _loadedKind = KindFromRow(row);
        MemberId = row.MemberId ?? string.Empty;
        Notes = row.Notes ?? string.Empty;
        OnPropertyChanged(nameof(IsMemberTab));
        BeginMemberIdEntryCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private static PosTabAccountKind KindFromRow(TabEditorRow row) =>
        row.IsGuest ? PosTabAccountKind.Guest : PosTabAccountKind.Member;

    private async Task BeginNameEntryAsync()
    {
        if (IsBusy || _loadFailed)
        {
            return;
        }

        var result = await _inputOverlay.ShowKeyboardAsync(TabName, "Tab name", CancellationToken.None).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        TabName = result;
    }

    private async Task BeginMemberIdEntryAsync()
    {
        if (IsBusy || _loadFailed || !IsMemberTab)
        {
            return;
        }

        var result = await _inputOverlay.ShowKeyboardAsync(MemberId, "Member id (optional)", CancellationToken.None).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        MemberId = result;
    }

    private async Task BeginNotesEntryAsync()
    {
        if (IsBusy || _loadFailed)
        {
            return;
        }

        var result = await _inputOverlay.ShowKeyboardAsync(Notes, "Notes", CancellationToken.None).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        Notes = result;
    }

    private void RequestDeleteTab()
    {
        if (!_editHost.CanDeleteCurrentTab || IsBusy || _loadFailed)
        {
            return;
        }

        _inputOverlay.Close();
        _editHost.RequestDeleteCurrentTab();
    }

    private void ClosePanel()
    {
        _inputOverlay.Close();
        _edit.Clear();
        _slide.Close();
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var previous = await _tabs.GetTabEditorRowAsync(_legacyId, CancellationToken.None).ConfigureAwait(true);
            if (previous is null)
            {
                StatusMessage = "Tab was not found.";
                return;
            }

            var r = await _tabs
                .UpdateTabAsync(_legacyId, TabName.Trim(), _loadedKind, Notes.Trim(), MemberId.Trim())
                .ConfigureAwait(true);
            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Could not save tab.";
                return;
            }

            var leg = _legacyId;
            var oldName = previous.DisplayName;
            var oldKind = KindFromRow(previous);
            var oldNotes = previous.Notes ?? string.Empty;
            var oldMember = previous.MemberId ?? string.Empty;

            _refreshBus.RequestRefresh();
            ClosePanel();

            _undo.PushUndo(
                "Undo last tab edit",
                async () =>
                {
                    var revert = await _tabs
                        .UpdateTabAsync(leg, oldName, oldKind, oldNotes, oldMember, CancellationToken.None)
                        .ConfigureAwait(true);
                    if (!revert.Ok)
                    {
                        return false;
                    }

                    _refreshBus.RequestRefresh();
                    return true;
                });
        }
        finally
        {
            IsBusy = false;
        }
    }
}
