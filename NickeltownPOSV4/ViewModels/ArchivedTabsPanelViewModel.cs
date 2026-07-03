using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class ArchivedTabsPanelViewModel : ObservableViewModel
{
    private const int PageSize = 6;

    private readonly ITabManagementRepository _tabs;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly ITabWorkspaceUndoStack _undo;

    private readonly ISlidePanelService _slide;

    private readonly IUserSessionService _session;

    private int _pageIndex;

    private int _totalCount;

    private string _pageLabel = "0 archived";

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    private ArchivedTabRowViewModel? _selected;

    private bool _eraseOverlayVisible;

    private string _eraseCaption = string.Empty;

    public ArchivedTabsPanelViewModel(
        ITabManagementRepository tabs,
        ITabWorkspaceRefreshBus refreshBus,
        ITabWorkspaceUndoStack undo,
        ISlidePanelService slide,
        IUserSessionService session)
    {
        _tabs = tabs;
        _refreshBus = refreshBus;
        _undo = undo;
        _slide = slide;
        _session = session;

        Items = new ObservableCollection<ArchivedTabRowViewModel>();

        CloseCommand = new RelayCommand(ClosePanel);
        RefreshCommand = new AsyncRelayCommand(LoadPageAsync, () => !IsBusy);
        PreviousPageCommand = new RelayCommand(() => PageIndex--, () => !IsBusy && PageIndex > 0);
        NextPageCommand = new RelayCommand(() => PageIndex++, () => !IsBusy && PageIndex < TotalPages - 1);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => !IsBusy && Selected is not null && _session.IsAdmin);
        PermanentDeleteCommand = new RelayCommand(BeginErase, CanPermanentDeleteExecute);
        ConfirmEraseCommand = new AsyncRelayCommand(ConfirmEraseAsync, () => !IsBusy && _eraseOverlayVisible);
        CancelEraseCommand = new RelayCommand(CancelErase, () => _eraseOverlayVisible);

        _session.PropertyChanged += (_, e) =>
        {
            if (e?.PropertyName is nameof(IUserSessionService.IsAdmin))
            {
                OnPropertyChanged(nameof(CanPermanentDelete));
                OnPropertyChanged(nameof(CanRestore));
                PermanentDeleteCommand.NotifyCanExecuteChanged();
                RestoreCommand.NotifyCanExecuteChanged();
            }
        };

        _ = LoadPageAsync();
    }

    public bool CanPermanentDelete => _session.IsAdmin;

    public bool CanRestore => _session.IsAdmin;

    public ObservableCollection<ArchivedTabRowViewModel> Items { get; }

    public ArchivedTabRowViewModel? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public int PageIndex
    {
        get => _pageIndex;
        set
        {
            var max = System.Math.Max(0, TotalPages - 1);
            var v = System.Math.Clamp(value, 0, max);
            if (!SetProperty(ref _pageIndex, v))
            {
                return;
            }

            _ = LoadPageAsync();
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
    }

    private int TotalPages => System.Math.Max(1, (int)System.Math.Ceiling(_totalCount / (double)PageSize));

    public string PageLabel
    {
        get => _pageLabel;
        private set => SetProperty(ref _pageLabel, value);
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
                RefreshCommand.NotifyCanExecuteChanged();
                RestoreCommand.NotifyCanExecuteChanged();
                PermanentDeleteCommand.NotifyCanExecuteChanged();
                PreviousPageCommand.NotifyCanExecuteChanged();
                NextPageCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IRelayCommand CloseCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand PreviousPageCommand { get; }

    public IRelayCommand NextPageCommand { get; }

    public IAsyncRelayCommand RestoreCommand { get; }

    public IRelayCommand PermanentDeleteCommand { get; }

    public IAsyncRelayCommand ConfirmEraseCommand { get; }

    public IRelayCommand CancelEraseCommand { get; }

    public bool EraseOverlayVisible
    {
        get => _eraseOverlayVisible;
        private set
        {
            if (SetProperty(ref _eraseOverlayVisible, value))
            {
                ConfirmEraseCommand.NotifyCanExecuteChanged();
                CancelEraseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string EraseCaption
    {
        get => _eraseCaption;
        private set => SetProperty(ref _eraseCaption, value);
    }

    private bool CanPermanentDeleteExecute() =>
        !IsBusy && Selected is not null && _session.IsAdmin && !_eraseOverlayVisible;

    private void ClosePanel() => _slide.Close();

    private async Task LoadPageAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            _totalCount = await _tabs.CountArchivedTabsAsync().ConfigureAwait(true);
            var rows = await _tabs.GetArchivedTabsPageAsync(_pageIndex, PageSize).ConfigureAwait(true);
            Items.Clear();
            foreach (var r in rows)
            {
                Items.Add(new ArchivedTabRowViewModel(r));
            }

            Selected = Items.FirstOrDefault();
            PageLabel = $"{_totalCount} archived · page {_pageIndex + 1}/{TotalPages}";
            PreviousPageCommand.NotifyCanExecuteChanged();
            NextPageCommand.NotifyCanExecuteChanged();
        }
        catch
        {
            StatusMessage = "Could not load archived tabs.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestoreAsync()
    {
        if (Selected is null || !_session.IsAdmin)
        {
            return;
        }

        var id = Selected.Source.LegacyId;
        var label = Selected.Source.DisplayLabel;
        IsBusy = true;
        try
        {
            var r = await _tabs.SetTabArchivedAsync(id, false).ConfigureAwait(true);
            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Could not restore tab.";
                return;
            }

            _undo.PushUndo(
                $"Undo restore ({label})",
                async () =>
                {
                    var back = await _tabs.SetTabArchivedAsync(id, true).ConfigureAwait(true);
                    if (!back.Ok)
                    {
                        return false;
                    }

                    _refreshBus.RequestRefresh();
                    return true;
                });

            _refreshBus.RequestRefresh();
            await LoadPageAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BeginErase()
    {
        if (Selected is null || !_session.IsAdmin)
        {
            return;
        }

        EraseCaption =
            $"“{Selected.Source.DisplayLabel}” and all of its drinks, payments, and history will be removed and cannot be recovered.";
        EraseOverlayVisible = true;
        PermanentDeleteCommand.NotifyCanExecuteChanged();
    }

    private void CancelErase()
    {
        EraseOverlayVisible = false;
        EraseCaption = string.Empty;
        PermanentDeleteCommand.NotifyCanExecuteChanged();
    }

    private async Task ConfirmEraseAsync()
    {
        if (Selected is null || !_session.IsAdmin)
        {
            return;
        }

        var id = Selected.Source.LegacyId;
        IsBusy = true;
        try
        {
            var r = await _tabs.PermanentDeleteTabAsync(id).ConfigureAwait(true);
            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Could not delete tab.";
                return;
            }

            CancelErase();
            _refreshBus.RequestRefresh();
            await LoadPageAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
