using System;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Views;

namespace NickeltownPOSV4.ViewModels;

public sealed class WorkspacePageViewModel : ObservableViewModel
{
    private readonly INavigationService _navigation;

    private readonly TabsWorkspaceViewModel _tabsWorkspace;

    private string _pageTitle = string.Empty;

    private string _subtitle = string.Empty;

    private string _body = string.Empty;

    private bool _isTabsRoute;

    private bool _isPitstopRoute;

    private bool _isMigrationEntryVisible;

    private bool _isStockManagementEntryVisible;

    private ShellRoute? _activeRoute;

    private IRelayCommand? _openMigrationCommand;

    public WorkspacePageViewModel(INavigationService navigation, TabsWorkspaceViewModel tabsWorkspace)
    {
        _navigation = navigation;
        _tabsWorkspace = tabsWorkspace;
        OpenStockManagementCommand = new RelayCommand(
            () => _navigation.Navigate(typeof(StockManagementPage)),
            () => IsStockManagementEntryVisible);
    }

    public IRelayCommand OpenMigrationCommand =>
        _openMigrationCommand ??= new RelayCommand(OpenMigration, () => IsMigrationEntryVisible && _activeRoute is not null);

    public IRelayCommand OpenStockManagementCommand { get; }

    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    public string Body
    {
        get => _body;
        private set => SetProperty(ref _body, value);
    }

    public bool IsTabsRoute
    {
        get => _isTabsRoute;
        private set => SetProperty(ref _isTabsRoute, value);
    }

    public bool IsPitstopRoute
    {
        get => _isPitstopRoute;
        private set => SetProperty(ref _isPitstopRoute, value);
    }

    public bool IsMigrationEntryVisible
    {
        get => _isMigrationEntryVisible;
        private set
        {
            if (SetProperty(ref _isMigrationEntryVisible, value))
            {
                OpenMigrationCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsStockManagementEntryVisible
    {
        get => _isStockManagementEntryVisible;
        private set
        {
            if (SetProperty(ref _isStockManagementEntryVisible, value))
            {
                OpenStockManagementCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public void ApplyRoute(ShellRoute route)
    {
        _activeRoute = route;
        IsTabsRoute = route.Id == "tabs";
        IsPitstopRoute = string.Equals(route.Id, "pitstop", StringComparison.OrdinalIgnoreCase);
        // Migration entry stays on Admin (Settings has its own Data import/export tile now).
        IsMigrationEntryVisible = !IsTabsRoute && string.Equals(route.Id, "admin", StringComparison.OrdinalIgnoreCase);
        IsStockManagementEntryVisible = string.Equals(route.Id, "admin", StringComparison.OrdinalIgnoreCase);

        if (IsTabsRoute)
        {
            _tabsWorkspace.ResetToTabsBoard();
            PageTitle = string.Empty;
            Subtitle = string.Empty;
            Body = string.Empty;
            return;
        }

        if (IsPitstopRoute)
        {
            PageTitle = string.Empty;
            Subtitle = string.Empty;
            Body = string.Empty;
            return;
        }

        PageTitle = route.Title;

        Subtitle = route.Id switch
        {
            "treasurer" => "Cash control, counts, and safe movements.",
            "reports" => "End-of-day and operational reporting.",
            "admin" => "Configuration, users, inventory, and store policies.",
            _ => string.Empty,
        };

        Body = route.Id switch
        {
            "treasurer" => "Cash pulls, loans, pickups, and blind counts should stay on full-screen pages (no pop-out windows) for kiosk clarity.",
            "reports" => "Operational summaries, flash reports, and audit trails can use wide cards and horizontal scroll on 1024x768.",
            "admin" => "Configuration and inventory: use Stock management (Admin workspace, or Tabs → More when signed in as admin).",
            _ => string.Empty,
        };
    }

    private void OpenMigration()
    {
        if (_activeRoute is null)
        {
            return;
        }

        _navigation.Navigate(typeof(MigrationPage), _activeRoute);
    }
}
