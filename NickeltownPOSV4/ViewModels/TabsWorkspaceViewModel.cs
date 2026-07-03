using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Tabs;
using NickeltownPOSV4.Views;
using NickeltownPOSV4.Views.Panels;
using NickeltownPOSV4.Views.Settings;

namespace NickeltownPOSV4.ViewModels;

/// <summary>3×3 paginated bar tabs board with mode segments (member / guest / archived).</summary>
public sealed class TabsWorkspaceViewModel : ObservableViewModel
{
    public const int FixedColumns = 3;

    public const int FixedRows = 3;

    public const int PageSize = FixedColumns * FixedRows;

    private readonly ITabWorkspaceQuery _tabQuery;

    private readonly ITabWorkspaceRefreshBus _refreshBus;

    private readonly IUserSessionService _session;

    private readonly ISlidePanelService _slidePanel;

    private readonly IServiceProvider _services;

    private readonly AddDrinksWorkspaceNavigator _addDrinksWorkspaceNavigator;

    private readonly ITabManagementRepository _tabManagement;

    private readonly ITabPanelTargetBinder _panelTargetBinder;

    private readonly ITabsManagementUndoService _tabsManagementUndo;

    private readonly IEditTabSession _editTabSession;

    private readonly IAddDrinksSession _addDrinksSession;

    private readonly IAddFundsSession _addFundsSession;

    private readonly ITabHistorySession _tabHistorySession;

    private readonly ITabWorkspaceUndoStack _undo;

    private readonly IRootNavigationCoordinator _rootNav;

    private readonly IAuthSignOutService _signOut;

    private readonly INavigationService _navigation;

    private readonly IGuestCloseoutOpenBus _guestCloseoutOpen;

    private readonly IInputOverlayService _inputOverlay;

    private readonly IAuthenticationService _authentication;

    private string? _lastSelectedTabId;

    private string _dataStatusHint = string.Empty;

    private string _operatorHint = string.Empty;

    private bool _archiveOverlayVisible;

    private string _archiveOverlayText = string.Empty;

    private bool _deleteOverlayVisible;

    private string _deleteOverlayText = string.Empty;

    public ObservableCollection<TabsBoardCellViewModel> PageSlots { get; } = new();

    private TabsBoardMode _boardMode = TabsBoardMode.OpenTabs;

    private int _openPage;

    private int _guestPage;

    private int _archivedPage;

    private string _pageInfo = "Page 1 of 1";

    private string _bartenderWelcome = string.Empty;

    private int _openTabsCount;

    private int _guestTabsCount;

    private int _archivedTabsCount;

    private TabCardModel? _selectedTab;

    private readonly List<TabCardModel> _fullOpenTabs = new();

    private readonly List<TabCardModel> _archivedTabCards = new();

    private bool _isAddDrinksWorkspaceOpen;

    private AddDrinksPanelViewModel? _addDrinksWorkspaceViewModel;

    public TabsWorkspaceViewModel(
        ITabWorkspaceQuery tabQuery,
        ITabWorkspaceRefreshBus refreshBus,
        IUserSessionService session,
        ISlidePanelService slidePanel,
        IServiceProvider services,
        AddDrinksWorkspaceNavigator addDrinksWorkspaceNavigator,
        ITabManagementRepository tabManagement,
        ITabPanelTargetBinder panelTargetBinder,
        ITabsManagementUndoService tabsManagementUndo,
        IEditTabSession editTabSession,
        IAddDrinksSession addDrinksSession,
        IAddFundsSession addFundsSession,
        ITabHistorySession tabHistorySession,
        ITabWorkspaceUndoStack undo,
        IRootNavigationCoordinator rootNav,
        INavigationService navigation,
        IGuestCloseoutOpenBus guestCloseoutOpen,
        IInputOverlayService inputOverlay,
        IAuthenticationService authentication,
        IAuthSignOutService signOut)
    {
        _tabQuery = tabQuery;
        _refreshBus = refreshBus;
        _session = session;
        _slidePanel = slidePanel;
        _services = services;
        _addDrinksWorkspaceNavigator = addDrinksWorkspaceNavigator;
        _tabManagement = tabManagement;
        _panelTargetBinder = panelTargetBinder;
        _tabsManagementUndo = tabsManagementUndo;
        _editTabSession = editTabSession;
        _addDrinksSession = addDrinksSession;
        _addFundsSession = addFundsSession;
        _tabHistorySession = tabHistorySession;
        _undo = undo;
        _rootNav = rootNav;
        _signOut = signOut;
        _navigation = navigation;
        _guestCloseoutOpen = guestCloseoutOpen;
        _inputOverlay = inputOverlay;
        _authentication = authentication;
        _guestCloseoutOpen.OpenRequested += OnGuestCloseoutOpenRequested;

        _session.PropertyChanged += OnShellSessionPropertyChanged;
        _refreshBus.RefreshRequested += OnTabsRefreshRequested;

        for (var i = 0; i < PageSize; i++)
        {
            PageSlots.Add(new TabsBoardCellViewModel());
        }

        UpdateWelcomeFromSession();
        PreviousPageCommand = new RelayCommand(GoPrevious, () => CurrentPage > 0);
        NextPageCommand = new RelayCommand(GoNext, () => CurrentPage < TotalPages - 1);
        SelectTabCommand = new RelayCommand<TabCardModel?>(SelectTab);
        SelectOpenTabsModeCommand = new RelayCommand(() => SetBoardMode(TabsBoardMode.OpenTabs));
        SelectGuestTabsModeCommand = new RelayCommand(() => SetBoardMode(TabsBoardMode.GuestTabs));
        SelectArchivedTabsModeCommand = new RelayCommand(() => SetBoardMode(TabsBoardMode.ArchivedTabs), () => _session.IsAdmin);
        OpenAddCardCommand = new RelayCommand(OpenAddCardForCurrentMode, () => ToolbarActionsEnabled);
        RestoreSelectedCommand = new AsyncRelayCommand(RestoreSelectedAsync, CanRestoreSelected);
        RefreshFromDatabaseCommand = new AsyncRelayCommand(RefreshTabsFromDatabaseAsync);
        NewTabCommand = new RelayCommand(OpenNewTabPanel, () => ToolbarActionsEnabled);
        GuestsCommand = new RelayCommand(OpenGuestTabPanel, () => ToolbarActionsEnabled);
        EditTabCommand = new RelayCommand(OpenEditTabPanel, CanEditTab);
        ArchivedTabsCommand = new RelayCommand(OpenArchivedTabsPanel, () => ToolbarActionsEnabled && _session.IsAdmin);
        ArchiveSelectedCommand = new RelayCommand(RequestArchiveSelected, CanArchiveSelected);
        DeleteSelectedCommand = new RelayCommand(RequestDeleteSelected, CanDeleteSelected);
        ConfirmArchiveCommand = new AsyncRelayCommand(ConfirmArchiveAsync, () => _archiveOverlayVisible && _session.IsAdmin);
        CancelArchiveCommand = new RelayCommand(CancelArchiveOverlay, () => _archiveOverlayVisible);
        ConfirmPermanentDeleteCommand = new AsyncRelayCommand(ConfirmPermanentDeleteAsync, () => _deleteOverlayVisible && _session.IsAdmin);
        CancelDeleteCommand = new RelayCommand(CancelDeleteOverlay, () => _deleteOverlayVisible);
        UndoLastCommand = new AsyncRelayCommand(UndoLastAsync, () => _undo.CanUndo);
        _undo.Changed += (_, _) =>
        {
            UndoLastCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(UndoToolTip));
        };
        InfoCommand = new RelayCommand(OpenInfoPanel, () => ToolbarActionsEnabled);
        SettingsCommand = new RelayCommand(OpenStoreSettings, () => ToolbarActionsEnabled);
        OpenDrinksPanelCommand = new RelayCommand(OpenDrinksPanel, CanOpenDrinksOrFunds);
        OpenFundsPanelCommand = new RelayCommand(OpenFundsPanel, CanOpenDrinksOrFunds);
        OpenTabHistoryPanelCommand = new RelayCommand(OpenTabHistoryPanel, CanOpenTabHistory);
        OpenMoreActionsCommand = new RelayCommand(OpenMoreActionsPanel, () => ToolbarActionsEnabled);
        OpenStockManagementCommand = new RelayCommand(OpenStockManagement, () => ToolbarActionsEnabled && _session.IsAdmin);
        OpenGuestCloseoutCommand = new RelayCommand(OpenGuestCloseoutPanel, () => ToolbarActionsEnabled);
        CloseOutGuestTabCommand = new RelayCommand(OpenGuestTabCloseoutPanel, CanCloseOutGuestTab);
        CloseSlidePanelCommand = new RelayCommand(CloseSlideAndAddDrinksOverlay);
        SignOutCommand = new RelayCommand(SignOutFromTabs);

        _addDrinksWorkspaceNavigator.SetHandler(OnAddDrinksWorkspaceCloseRequested);
    }

    private bool ToolbarActionsEnabled =>
        TabsWorkspaceCommandGuards.ToolbarActionsEnabled(
            _archiveOverlayVisible,
            _deleteOverlayVisible,
            IsAddDrinksWorkspaceOpen);

    public bool CanAdminDeleteTab => _session.IsAdmin;

    public bool CanAdminArchiveTab => _session.IsAdmin;

    public bool CanViewArchivedTabs => _session.IsAdmin;

    public bool ShowStockManagementEntry => _session.IsAdmin;

    public string UndoToolTip =>
        _undo.CanUndo ? (_undo.UndoDescription ?? "Undo last tab action") : "Nothing to undo";

    public bool ArchiveOverlayVisible
    {
        get => _archiveOverlayVisible;
        private set
        {
            if (SetProperty(ref _archiveOverlayVisible, value))
            {
                NotifyToolbarCommands();
            }
        }
    }

    public string ArchiveOverlayText
    {
        get => _archiveOverlayText;
        private set => SetProperty(ref _archiveOverlayText, value);
    }

    public bool DeleteOverlayVisible
    {
        get => _deleteOverlayVisible;
        private set
        {
            if (SetProperty(ref _deleteOverlayVisible, value))
            {
                NotifyToolbarCommands();
            }
        }
    }

    public string DeleteOverlayText
    {
        get => _deleteOverlayText;
        private set => SetProperty(ref _deleteOverlayText, value);
    }

    public bool IsAddDrinksWorkspaceOpen
    {
        get => _isAddDrinksWorkspaceOpen;
        private set
        {
            if (SetProperty(ref _isAddDrinksWorkspaceOpen, value))
            {
                NotifyToolbarCommands();
            }
        }
    }

    public AddDrinksPanelViewModel? CurrentAddDrinksViewModel => _addDrinksWorkspaceViewModel;

    /// <summary>Short operator message or selected tab label only (no live-data wall of text).</summary>
    public string ToolbarHintLine =>
        TabsBoardHintsHelper.FormatToolbarHintLine(_operatorHint, SelectedTab);

    public bool HasToolbarHint => !string.IsNullOrWhiteSpace(ToolbarHintLine);

    private void RaiseToolbarHeaderHints()
    {
        OnPropertyChanged(nameof(ToolbarHintLine));
        OnPropertyChanged(nameof(HasToolbarHint));
    }

    private void SignOutFromTabs()
    {
        ShellCloseSlideAndAddDrinks();
        _signOut.SignOut(clearTabUndo: true);
    }

    private void OnShellSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IUserSessionService.DisplayName) or nameof(IUserSessionService.IsSignedIn))
        {
            UpdateWelcomeFromSession();
        }

        if (e.PropertyName is nameof(IUserSessionService.IsAdmin))
        {
            OnPropertyChanged(nameof(CanAdminDeleteTab));
            OnPropertyChanged(nameof(CanAdminArchiveTab));
            OnPropertyChanged(nameof(CanViewArchivedTabs));
            OnPropertyChanged(nameof(ShowStockManagementEntry));
            DeleteSelectedCommand.NotifyCanExecuteChanged();
            ConfirmPermanentDeleteCommand.NotifyCanExecuteChanged();
            ArchiveSelectedCommand.NotifyCanExecuteChanged();
            ConfirmArchiveCommand.NotifyCanExecuteChanged();
            SelectArchivedTabsModeCommand.NotifyCanExecuteChanged();
            ArchivedTabsCommand.NotifyCanExecuteChanged();
            RestoreSelectedCommand.NotifyCanExecuteChanged();
            OpenStockManagementCommand.NotifyCanExecuteChanged();

            if (!_session.IsAdmin && BoardMode == TabsBoardMode.ArchivedTabs)
            {
                SetBoardMode(TabsBoardMode.OpenTabs);
            }
        }
    }

    private void UpdateWelcomeFromSession() =>
        BartenderWelcome = TabsBoardHintsHelper.FormatWelcomeText(_session.IsSignedIn, _session.DisplayName);

    private void OnTabsRefreshRequested(object? sender, EventArgs e) =>
        _ = RefreshTabsFromDatabaseAsync();

    private void OnGuestCloseoutOpenRequested(object? sender, EventArgs e) => OpenGuestCloseoutPanel();

    public IAsyncRelayCommand RefreshFromDatabaseCommand { get; }

    public IRelayCommand PreviousPageCommand { get; }

    public IRelayCommand NextPageCommand { get; }

    public IRelayCommand SelectOpenTabsModeCommand { get; }

    public IRelayCommand SelectGuestTabsModeCommand { get; }

    public IRelayCommand SelectArchivedTabsModeCommand { get; }

    public IRelayCommand OpenAddCardCommand { get; }

    public IRelayCommand<TabCardModel?> SelectTabCommand { get; }

    public IAsyncRelayCommand RestoreSelectedCommand { get; }

    public IRelayCommand NewTabCommand { get; }

    public IRelayCommand GuestsCommand { get; }

    public IRelayCommand EditTabCommand { get; }

    public IRelayCommand ArchivedTabsCommand { get; }

    public IRelayCommand ArchiveSelectedCommand { get; }

    public IRelayCommand DeleteSelectedCommand { get; }

    public IAsyncRelayCommand ConfirmArchiveCommand { get; }

    public IRelayCommand CancelArchiveCommand { get; }

    public IAsyncRelayCommand ConfirmPermanentDeleteCommand { get; }

    public IRelayCommand CancelDeleteCommand { get; }

    public IAsyncRelayCommand UndoLastCommand { get; }

    public IRelayCommand InfoCommand { get; }

    public IRelayCommand SettingsCommand { get; }

    public IRelayCommand OpenDrinksPanelCommand { get; }

    public IRelayCommand OpenFundsPanelCommand { get; }

    public IRelayCommand OpenTabHistoryPanelCommand { get; }

    public IRelayCommand OpenMoreActionsCommand { get; }

    public IRelayCommand OpenStockManagementCommand { get; }

    public IRelayCommand OpenGuestCloseoutCommand { get; }

    public IRelayCommand CloseOutGuestTabCommand { get; }

    public IRelayCommand CloseSlidePanelCommand { get; }

    public IRelayCommand SignOutCommand { get; }

    public async Task RefreshTabsFromDatabaseAsync()
    {
        try
        {
            var rows = await _tabQuery.GetOpenTabCardsAsync().ConfigureAwait(true);
            _fullOpenTabs.Clear();
            _fullOpenTabs.AddRange(TabsBoardCatalogHelper.SortForBoard(rows));
            DataStatusHint = TabsBoardHintsHelper.BuildLiveTabsStatusHint(_fullOpenTabs);

            await LoadArchivedTabCardsAsync().ConfigureAwait(true);
            UpdateModeCounts();
            RebuildBoardPage();
            TryReselectTabAfterReload();
        }
        catch
        {
            _fullOpenTabs.Clear();
            _archivedTabCards.Clear();
            DataStatusHint = TabsBoardHintsHelper.SqliteErrorMessage;
            UpdateModeCounts();
            RebuildBoardPage();
            TryReselectTabAfterReload();
        }

        RaiseToolbarHeaderHints();
        RaiseActionBarProperties();
    }

    private async Task LoadArchivedTabCardsAsync()
    {
        _archivedTabCards.Clear();
        try
        {
            var count = await _tabManagement.CountArchivedTabsAsync().ConfigureAwait(true);
            if (count <= 0)
            {
                return;
            }

            var rows = await _tabManagement.GetArchivedTabsPageAsync(0, count).ConfigureAwait(true);
            foreach (var row in rows)
            {
                _archivedTabCards.Add(new TabCardModel(
                    row.LegacyId,
                    row.DisplayLabel,
                    row.Balance,
                    row.LastActivityText,
                    isMember: true,
                    isGuest: false));
            }
        }
        catch
        {
            // Archived list is optional for board display; leave empty.
        }
    }

    private void TryReselectTabAfterReload()
    {
        if (string.IsNullOrEmpty(_lastSelectedTabId))
        {
            return;
        }

        var match = FindTabInCurrentMode(_lastSelectedTabId);
        if (match is not null)
        {
            SelectTab(match);
            return;
        }

        ClearAllTabSelections();
        SelectedTab = null;
        SyncBoardCellSelection();
    }

    public string PageTitle => TabsBoardHintsHelper.BarTabsPageTitle;

    public string BartenderWelcome
    {
        get => _bartenderWelcome;
        private set => SetProperty(ref _bartenderWelcome, value);
    }

    public TabsBoardMode BoardMode
    {
        get => _boardMode;
        private set
        {
            if (SetProperty(ref _boardMode, value))
            {
                OnPropertyChanged(nameof(IsOpenMode));
                OnPropertyChanged(nameof(IsGuestMode));
                OnPropertyChanged(nameof(IsArchivedMode));
                OnPropertyChanged(nameof(ArchiveOrRestoreButtonLabel));
                ArchiveSelectedCommand.NotifyCanExecuteChanged();
                RestoreSelectedCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsOpenMode => BoardMode == TabsBoardMode.OpenTabs;

    public bool IsGuestMode => BoardMode == TabsBoardMode.GuestTabs;

    public bool IsArchivedMode => BoardMode == TabsBoardMode.ArchivedTabs;

    public int OpenTabsCount
    {
        get => _openTabsCount;
        private set => SetProperty(ref _openTabsCount, value);
    }

    public int GuestTabsCount
    {
        get => _guestTabsCount;
        private set => SetProperty(ref _guestTabsCount, value);
    }

    public int ArchivedTabsCount
    {
        get => _archivedTabsCount;
        private set => SetProperty(ref _archivedTabsCount, value);
    }

    public string ArchiveOrRestoreButtonLabel =>
        IsArchivedMode ? "Restore Tab" : "Archive Tab";

    public string ActionBarPlaceholder => "Select a tab to get started";

    public string SelectedTabName => SelectedTab?.DisplayName ?? string.Empty;

    public string SelectedTabBadge => SelectedTab?.MemberBadge ?? string.Empty;

    public string SelectedBalanceText => SelectedTab?.BalanceText ?? string.Empty;

    public string SelectedStatusLabel => SelectedTab?.BalanceStatusLabel ?? string.Empty;

    public string SelectedLastUpdated => SelectedTab?.LastUpdatedText ?? string.Empty;

    public string SelectedDrinksLine => SelectedTab?.LastDrinkLine ?? string.Empty;

    public TabBalanceTier SelectedBalanceTier => SelectedTab?.BalanceTier ?? TabBalanceTier.Good;

    public string PageInfo
    {
        get => _pageInfo;
        private set => SetProperty(ref _pageInfo, value);
    }

    public int CurrentPage =>
        BoardMode switch
        {
            TabsBoardMode.GuestTabs => _guestPage,
            TabsBoardMode.ArchivedTabs => _archivedPage,
            _ => _openPage,
        };

    private bool ShowsAddCard => BoardMode is TabsBoardMode.OpenTabs or TabsBoardMode.GuestTabs;

    private int TotalPages =>
        TabsBoardPagerHelper.TotalPages(GetTabsForCurrentMode().Count(), ShowsAddCard);

    public string DataStatusHint
    {
        get => _dataStatusHint;
        private set => SetProperty(ref _dataStatusHint, value);
    }

    public string OperatorHint
    {
        get => _operatorHint;
        set
        {
            if (SetProperty(ref _operatorHint, value ?? string.Empty))
            {
                RaiseToolbarHeaderHints();
            }
        }
    }

    public TabCardModel? SelectedTab
    {
        get => _selectedTab;
        private set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                EditTabCommand.NotifyCanExecuteChanged();
                ArchiveSelectedCommand.NotifyCanExecuteChanged();
                DeleteSelectedCommand.NotifyCanExecuteChanged();
                OpenDrinksPanelCommand.NotifyCanExecuteChanged();
                OpenFundsPanelCommand.NotifyCanExecuteChanged();
                OpenTabHistoryPanelCommand.NotifyCanExecuteChanged();
                CloseOutGuestTabCommand.NotifyCanExecuteChanged();
                RestoreSelectedCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(HasSelectedTab));
                RaiseToolbarHeaderHints();
                RaiseActionBarProperties();
            }
        }
    }

    public bool HasSelectedTab => SelectedTab is not null;

    public bool ShowGuestTabActions => HasSelectedTab && SelectedTab!.IsGuest && !IsArchivedMode;

    public bool ShowMemberTabActions => HasSelectedTab && SelectedTab is { IsGuest: false } && !IsArchivedMode;

    private void RaiseActionBarProperties()
    {
        OnPropertyChanged(nameof(SelectedTabName));
        OnPropertyChanged(nameof(SelectedTabBadge));
        OnPropertyChanged(nameof(SelectedBalanceText));
        OnPropertyChanged(nameof(SelectedStatusLabel));
        OnPropertyChanged(nameof(SelectedLastUpdated));
        OnPropertyChanged(nameof(SelectedDrinksLine));
        OnPropertyChanged(nameof(SelectedBalanceTier));
        OnPropertyChanged(nameof(ShowGuestTabActions));
        OnPropertyChanged(nameof(ShowMemberTabActions));
        CloseOutGuestTabCommand.NotifyCanExecuteChanged();
    }

    public void SelectOpenTabById(string? tabId)
    {
        if (string.IsNullOrEmpty(tabId))
        {
            SelectOpenTab(null);
            return;
        }

        SelectOpenTab(TabsBoardSelectionHelper.FindById(_fullOpenTabs, tabId));
    }

    public void SelectOpenTab(TabCardModel? tab)
    {
        ClearAllTabSelections();
        if (tab is not null)
        {
            tab.IsSelected = true;
        }

        SelectedTab = tab;
        _lastSelectedTabId = tab?.Id;
        SyncBoardCellSelection();
        OperatorHint = string.Empty;
        RaiseToolbarHeaderHints();
    }

    private void ClearAllTabSelections()
    {
        foreach (var t in _fullOpenTabs)
        {
            t.IsSelected = false;
        }

        foreach (var t in _archivedTabCards)
        {
            t.IsSelected = false;
        }
    }

    private void SyncBoardCellSelection()
    {
        foreach (var cell in PageSlots.Where(c => c.IsTabCell))
        {
            cell.NotifySelectionChanged();
        }
    }

    private void GoPrevious()
    {
        if (CurrentPage > 0)
        {
            SetCurrentPage(CurrentPage - 1);
        }
    }

    private void GoNext()
    {
        if (CurrentPage < TotalPages - 1)
        {
            SetCurrentPage(CurrentPage + 1);
        }
    }

    private void SetCurrentPage(int page)
    {
        var clamped = TabsBoardPagerHelper.ClampPage(page, GetTabsForCurrentMode().Count(), ShowsAddCard);
        switch (BoardMode)
        {
            case TabsBoardMode.GuestTabs:
                _guestPage = clamped;
                break;
            case TabsBoardMode.ArchivedTabs:
                _archivedPage = clamped;
                break;
            default:
                _openPage = clamped;
                break;
        }

        OnPropertyChanged(nameof(CurrentPage));
        RebuildBoardPage();
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
    }

    private void SelectTab(TabCardModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        SelectOpenTab(tab);
    }

    private void UpdateModeCounts()
    {
        OpenTabsCount = TabsBoardCatalogHelper.MemberTabsOnly(_fullOpenTabs).Count();
        GuestTabsCount = _fullOpenTabs.Count(t => t.IsGuest);
        ArchivedTabsCount = _archivedTabCards.Count;
        OnPropertyChanged(nameof(OpenTabsCount));
        OnPropertyChanged(nameof(GuestTabsCount));
        OnPropertyChanged(nameof(ArchivedTabsCount));
    }

    private void SetBoardMode(TabsBoardMode mode)
    {
        if (mode == TabsBoardMode.ArchivedTabs && !_session.IsAdmin)
        {
            mode = TabsBoardMode.OpenTabs;
        }

        if (_boardMode == mode)
        {
            return;
        }

        var previousTabId = _lastSelectedTabId;
        BoardMode = mode;
        RebuildBoardPage();

        if (string.IsNullOrEmpty(previousTabId))
        {
            ClearAllTabSelections();
            SelectedTab = null;
        }
        else
        {
            var match = FindTabInCurrentMode(previousTabId);
            if (match is not null)
            {
                SelectOpenTab(match);
            }
            else
            {
                ClearAllTabSelections();
                SelectedTab = null;
                _lastSelectedTabId = null;
            }
        }

        RaiseActionBarProperties();
        OpenAddCardCommand.NotifyCanExecuteChanged();
    }

    private IEnumerable<TabCardModel> GetTabsForCurrentMode()
    {
        IEnumerable<TabCardModel> source = BoardMode switch
        {
            TabsBoardMode.GuestTabs => _fullOpenTabs.Where(t => t.IsGuest),
            TabsBoardMode.ArchivedTabs => _archivedTabCards,
            _ => TabsBoardCatalogHelper.MemberTabsOnly(_fullOpenTabs),
        };

        return source.OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    private TabCardModel? FindTabInCurrentMode(string? tabId)
    {
        if (string.IsNullOrEmpty(tabId))
        {
            return null;
        }

        return TabsBoardSelectionHelper.FindById(GetTabsForCurrentMode().ToList(), tabId);
    }

    private void RebuildBoardPage()
    {
        var tabs = GetTabsForCurrentMode().ToList();
        var includeAdd = ShowsAddCard;
        var page = TabsBoardPagerHelper.ClampPage(CurrentPage, tabs.Count, includeAdd);
        switch (BoardMode)
        {
            case TabsBoardMode.GuestTabs:
                _guestPage = page;
                break;
            case TabsBoardMode.ArchivedTabs:
                _archivedPage = page;
                break;
            default:
                _openPage = page;
                break;
        }

        OnPropertyChanged(nameof(CurrentPage));

        var addKind = BoardMode == TabsBoardMode.GuestTabs
            ? TabsBoardCellKind.NewGuestTab
            : TabsBoardCellKind.NewMemberTab;

        TabsBoardPagerHelper.ApplyPageToBoardSlots(
            PageSlots,
            tabs,
            page,
            includeAdd,
            addKind);

        PageInfo = TabsBoardPagerHelper.FormatPageInfo(
            page,
            TabsBoardPagerHelper.TotalPages(tabs.Count, includeAdd));
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        SyncBoardCellSelection();
    }

    public bool CanDeleteSelectedForEditPanel() => CanDeleteSelected();

    public void RequestDeleteFromEditPanel()
    {
        _slidePanel.Close();
        _editTabSession.Clear();
        RequestDeleteSelected();
    }

    private void OpenAddCardForCurrentMode()
    {
        if (BoardMode == TabsBoardMode.OpenTabs)
        {
            OpenNewTabPanel();
            return;
        }

        if (BoardMode == TabsBoardMode.GuestTabs)
        {
            OpenGuestTabPanel();
        }
    }

    private void NotifyToolbarCommands()
    {
        NewTabCommand.NotifyCanExecuteChanged();
        GuestsCommand.NotifyCanExecuteChanged();
        EditTabCommand.NotifyCanExecuteChanged();
        ArchivedTabsCommand.NotifyCanExecuteChanged();
        ArchiveSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        InfoCommand.NotifyCanExecuteChanged();
        SettingsCommand.NotifyCanExecuteChanged();
        OpenDrinksPanelCommand.NotifyCanExecuteChanged();
        OpenFundsPanelCommand.NotifyCanExecuteChanged();
        OpenTabHistoryPanelCommand.NotifyCanExecuteChanged();
        OpenMoreActionsCommand.NotifyCanExecuteChanged();
        OpenStockManagementCommand.NotifyCanExecuteChanged();
        OpenGuestCloseoutCommand.NotifyCanExecuteChanged();
        CloseOutGuestTabCommand.NotifyCanExecuteChanged();
        UndoLastCommand.NotifyCanExecuteChanged();
        OpenAddCardCommand.NotifyCanExecuteChanged();
        RestoreSelectedCommand.NotifyCanExecuteChanged();
    }

    private bool CanRestoreSelected() =>
        TabsWorkspaceCommandGuards.CanRestoreSelected(ToolbarActionsEnabled, _session.IsAdmin, SelectedTab)
        && IsArchivedMode;

    private async Task RestoreSelectedAsync()
    {
        if (!_session.IsAdmin)
        {
            OperatorHint = "Archived tabs are restricted to admins.";
            RaiseToolbarHeaderHints();
            return;
        }

        if (SelectedTab is null || !IsArchivedMode)
        {
            OperatorHint = "Select an archived tab to restore.";
            RaiseToolbarHeaderHints();
            return;
        }

        var id = SelectedTab.Id;
        var label = SelectedTab.DisplayName;
        var r = await _tabManagement.SetTabArchivedAsync(id, false).ConfigureAwait(true);
        if (!r.Ok)
        {
            OperatorHint = r.ErrorMessage ?? "Could not restore tab.";
            RaiseToolbarHeaderHints();
            return;
        }

        _tabsManagementUndo.RegisterArchiveUndo(id, label);
        _refreshBus.RequestRefresh();
        await RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
    }

    /// <summary>Tab board home: closes add-drinks overlay and slide panels (e.g. when Tabs bottom-nav is selected).</summary>
    public void ResetToTabsBoard() => ShellCloseSlideAndAddDrinks();

    private void ShellCloseSlideAndAddDrinks()
    {
        _slidePanel.Close();
        ForceCloseAddDrinksWorkspace();
    }

    /// <summary>Opens or swaps slide-panel content without closing first (avoids close-then-open animation races).</summary>
    private void OpenSlidePanel(UserControl content, double? panelWidthPixels = null)
    {
        ForceCloseAddDrinksWorkspace();
        _slidePanel.Open(content, panelWidthPixels);
    }

    private void ForceCloseAddDrinksWorkspace()
    {
        if (!_isAddDrinksWorkspaceOpen)
        {
            return;
        }

        _addDrinksSession.Clear();
        _addDrinksWorkspaceViewModel = null;
        OnPropertyChanged(nameof(CurrentAddDrinksViewModel));
        IsAddDrinksWorkspaceOpen = false;
    }

    private void OnAddDrinksWorkspaceCloseRequested()
    {
        if (!_isAddDrinksWorkspaceOpen)
        {
            return;
        }

        _addDrinksWorkspaceViewModel = null;
        OnPropertyChanged(nameof(CurrentAddDrinksViewModel));
        IsAddDrinksWorkspaceOpen = false;
    }

    private void CloseSlideAndAddDrinksOverlay() => ShellCloseSlideAndAddDrinks();

    private bool CanOpenDrinksOrFunds() =>
        TabsWorkspaceCommandGuards.CanOpenDrinksOrFunds(ToolbarActionsEnabled, SelectedTab);

    /// <summary>Selects a tab by board id and opens the add-drinks workspace (used from guest panels).</summary>
    public void OpenDrinksForTab(string tabId)
    {
        SelectOpenTabById(tabId);
        if (SelectedTab is null)
        {
            OperatorHint = "Guest tab was not found — refresh and try again.";
            RaiseToolbarHeaderHints();
            return;
        }

        OpenDrinksPanel();
    }

    /// <summary>Selects a tab by board id and opens the add-funds slide panel (used from guest panels).</summary>
    public void OpenFundsForTab(string tabId)
    {
        SelectOpenTabById(tabId);
        if (SelectedTab is null)
        {
            OperatorHint = "Guest tab was not found — refresh and try again.";
            RaiseToolbarHeaderHints();
            return;
        }

        OpenFundsPanel();
    }

    private void OpenDrinksPanel()
    {
        ShellCloseSlideAndAddDrinks();
        if (SelectedTab is { } tab)
        {
            _panelTargetBinder.BindFromTab(tab, withGuestFlag: true);
        }
        else
        {
            _panelTargetBinder.Bind(null, null, null, false);
        }

        _addDrinksWorkspaceViewModel = _services.GetRequiredService<AddDrinksPanelViewModel>();
        OnPropertyChanged(nameof(CurrentAddDrinksViewModel));
        IsAddDrinksWorkspaceOpen = true;
        _ = _addDrinksWorkspaceViewModel.RefreshCatalogFromDatabaseAsync();
    }

    private void OpenFundsPanel()
    {
        if (SelectedTab is { } tab)
        {
            _panelTargetBinder.BindFromTab(tab);
        }
        else
        {
            _panelTargetBinder.Bind(null, null, null);
        }

        OpenSlidePanel(_services.GetRequiredService<AddFundsPanel>());
    }

    private bool CanOpenTabHistory() =>
        TabsWorkspaceCommandGuards.CanOpenTabHistory(ToolbarActionsEnabled);

    private void OpenTabHistoryPanel()
    {
        if (SelectedTab is null)
        {
            OperatorHint = "Select a member tab on the board or a guest under Guests, then open Tab history.";
            RaiseToolbarHeaderHints();
            return;
        }

        _panelTargetBinder.Bind(SelectedTab.Id, SelectedTab.DisplayName);
        OpenSlidePanel(_services.GetRequiredService<TabHistoryPanel>(), 820);
    }

    private void OpenMoreActionsPanel()
    {
        var panel = _services.GetRequiredService<TabsMoreActionsPanel>();
        panel.DataContext = this;
        OpenSlidePanel(panel);
    }

    private void OpenStockManagement()
    {
        ShellCloseSlideAndAddDrinks();
        _navigation.Navigate(typeof(StockManagementPage));
    }

    private void OpenStoreSettings()
    {
        ShellCloseSlideAndAddDrinks();
        _navigation.Navigate(typeof(SettingsPage));
    }

    private void OpenNewTabPanel()
    {
        OpenSlidePanel(_services.GetRequiredService<NewTabPanel>());
    }

    private void OpenGuestTabPanel()
    {
        OpenSlidePanel(_services.GetRequiredService<GuestTabPanel>());
    }

    private void OpenGuestCloseoutPanel()
    {
        OpenSlidePanel(_services.GetRequiredService<GuestCloseoutPanel>());
    }

    private bool CanCloseOutGuestTab() =>
        TabsWorkspaceCommandGuards.CanOpenDrinksOrFunds(ToolbarActionsEnabled, SelectedTab)
        && SelectedTab is { IsGuest: true }
        && !IsArchivedMode;

    private void OpenGuestTabCloseoutPanel()
    {
        if (SelectedTab is not { IsGuest: true } tab)
        {
            return;
        }

        _panelTargetBinder.BindFromTab(tab, withGuestFlag: true);
        OpenSlidePanel(_services.GetRequiredService<GuestTabCloseoutPanel>());
    }

    private bool CanEditTab() =>
        TabsWorkspaceCommandGuards.CanEditTab(ToolbarActionsEnabled);

    private void OpenEditTabPanel()
    {
        if (SelectedTab is null)
        {
            OperatorHint = "Select a tab first.";
            RaiseToolbarHeaderHints();
            return;
        }

        _panelTargetBinder.Bind(SelectedTab.Id, SelectedTab.DisplayName);
        OpenSlidePanel(_services.GetRequiredService<EditTabPanel>());
    }

    private void OpenArchivedTabsPanel()
    {
        OpenSlidePanel(_services.GetRequiredService<ArchivedTabsPanel>());
    }

    private bool CanArchiveSelected() =>
        TabsWorkspaceCommandGuards.CanArchiveSelected(ToolbarActionsEnabled, _session.IsAdmin, SelectedTab)
        && !IsArchivedMode;

    private void RequestArchiveSelected()
    {
        if (!_session.IsAdmin)
        {
            OperatorHint = "Archiving tabs is restricted to admins.";
            RaiseToolbarHeaderHints();
            return;
        }

        if (SelectedTab is null)
        {
            OperatorHint = "Select a tab to archive.";
            RaiseToolbarHeaderHints();
            return;
        }

        ShellCloseSlideAndAddDrinks();

        ArchiveOverlayText = TabsOverlayTextHelper.ArchiveConfirm(SelectedTab.DisplayName);
        ArchiveOverlayVisible = true;
        ConfirmArchiveCommand.NotifyCanExecuteChanged();
        CancelArchiveCommand.NotifyCanExecuteChanged();
    }

    private async Task ConfirmArchiveAsync()
    {
        if (!_session.IsAdmin || SelectedTab is null)
        {
            return;
        }

        var id = SelectedTab.Id;
        var label = SelectedTab.DisplayName;
        var r = await _tabManagement.SetTabArchivedAsync(id, true).ConfigureAwait(true);
        if (!r.Ok)
        {
            OperatorHint = r.ErrorMessage ?? "Could not archive tab.";
            ArchiveOverlayVisible = false;
            return;
        }

        _tabsManagementUndo.RegisterArchiveUndo(id, label);

        ArchiveOverlayVisible = false;
        _refreshBus.RequestRefresh();
        await RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
    }

    private void CancelArchiveOverlay()
    {
        ArchiveOverlayVisible = false;
        ArchiveOverlayText = string.Empty;
        ConfirmArchiveCommand.NotifyCanExecuteChanged();
        CancelArchiveCommand.NotifyCanExecuteChanged();
    }

    private bool CanDeleteSelected() =>
        TabsWorkspaceCommandGuards.CanDeleteSelected(
            ToolbarActionsEnabled,
            _session.IsAdmin,
            SelectedTab);

    private void RequestDeleteSelected()
    {
        if (!_session.IsAdmin)
        {
            return;
        }

        if (SelectedTab is null)
        {
            OperatorHint = "Select a tab first.";
            RaiseToolbarHeaderHints();
            return;
        }

        ShellCloseSlideAndAddDrinks();
        DeleteOverlayText = TabsOverlayTextHelper.DeleteConfirm(SelectedTab.DisplayName);
        DeleteOverlayVisible = true;
        RaiseDeleteCommandStates();
    }

    private void RaiseDeleteCommandStates()
    {
        ConfirmPermanentDeleteCommand.NotifyCanExecuteChanged();
        CancelDeleteCommand.NotifyCanExecuteChanged();
    }

    private async Task ConfirmPermanentDeleteAsync()
    {
        if (!_session.IsAdmin || SelectedTab is null)
        {
            return;
        }

        var id = SelectedTab.Id;
        var r = await _tabManagement.PermanentDeleteTabAsync(id).ConfigureAwait(true);
        if (!r.Ok)
        {
            OperatorHint = r.ErrorMessage ?? "Could not erase tab.";
            CancelDeleteOverlay();
            return;
        }

        _undo.Clear();
        CancelDeleteOverlay();
        _refreshBus.RequestRefresh();
        await RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
    }

    private void CancelDeleteOverlay()
    {
        DeleteOverlayVisible = false;
        DeleteOverlayText = string.Empty;
        RaiseDeleteCommandStates();
    }

    private async Task UndoLastAsync()
    {
        ShellCloseSlideAndAddDrinks();
        if (!_undo.CanUndo)
        {
            OperatorHint = TabsUndoUiHelper.NothingToUndoMessage;
            RaiseToolbarHeaderHints();
            return;
        }

        if (!_session.IsSignedIn || _session.ActiveStaffId is null)
        {
            OperatorHint = "Sign in to undo.";
            RaiseToolbarHeaderHints();
            return;
        }

        var pin = await _inputOverlay
            .ShowPinNumpadAsync("Enter your PIN to undo", digitCount: 4, maskDisplay: true, CancellationToken.None)
            .ConfigureAwait(true);
        if (pin is null)
        {
            return;
        }

        var auth = await _authentication.AuthenticateByPinAsync(pin, CancellationToken.None).ConfigureAwait(true);
        if (!auth.Ok)
        {
            OperatorHint = auth.ErrorMessage ?? "Incorrect PIN.";
            RaiseToolbarHeaderHints();
            return;
        }

        if (auth.StaffPk != _session.ActiveStaffId.Value)
        {
            OperatorHint = "PIN must match the signed-in bartender.";
            RaiseToolbarHeaderHints();
            return;
        }

        var desc = _undo.UndoDescription;
        OperatorHint = string.Empty;
        RaiseToolbarHeaderHints();
        var ok = await _undo.TryUndoAsync().ConfigureAwait(true);
        OperatorHint = TabsUndoUiHelper.FormatUndoResult(ok, desc);

        await RefreshTabsFromDatabaseAsync().ConfigureAwait(true);
        UndoLastCommand.NotifyCanExecuteChanged();
        RaiseToolbarHeaderHints();
    }

    private void OpenInfoPanel()
    {
        OpenSlidePanel(_services.GetRequiredService<BarModeHelpPanel>());
    }
}
