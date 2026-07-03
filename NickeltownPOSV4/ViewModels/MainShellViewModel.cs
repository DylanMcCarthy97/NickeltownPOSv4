using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Tabs;
using NickeltownPOSV4.Views;

namespace NickeltownPOSV4.ViewModels;

public sealed class MainShellViewModel : ObservableViewModel
{
    private readonly INavigationService _navigation;

    private readonly WorkspacePageViewModel _workspace;

    private readonly IUserSessionService _session;

    private readonly IRootNavigationCoordinator _rootNav;

    private readonly ISlidePanelService _slide;

    private readonly ISquareConfigService _squareConfig;

    private readonly IAuthSignOutService _signOut;

    private string _headerTitle = "Nickeltown POS";

    private ShellRoute? _selectedRoute;

    private string _footerDateText = string.Empty;

    private string _footerTimeText = string.Empty;

    private string _squareStatusText = "Square";

    private bool _squareIsOnline;

    private DispatcherQueueTimer? _footerClockTimer;

    private DispatcherQueueTimer? _squareStatusTimer;

    public MainShellViewModel(
        INavigationService navigation,
        WorkspacePageViewModel workspace,
        IUserSessionService session,
        IRootNavigationCoordinator rootNav,
        ISlidePanelService slide,
        ISquareConfigService squareConfig,
        IAuthSignOutService signOut)
    {
        _navigation = navigation;
        _workspace = workspace;
        _session = session;
        _rootNav = rootNav;
        _slide = slide;
        _squareConfig = squareConfig;
        _signOut = signOut;

        Routes = new ObservableCollection<ShellRoute>(
        [
            new ShellRoute { Id = "tabs", Title = "Tabs", Glyph = "\uE8FD" },
            new ShellRoute { Id = "pitstop", Title = "Pitstop", Glyph = "\uE945" },
            // new ShellRoute { Id = "treasurer", Title = "Treasurer", Glyph = "\uE8C7" },
            new ShellRoute { Id = "reports", Title = "Reports", Glyph = "\uE9F9" },
            new ShellRoute { Id = "admin", Title = "Admin", Glyph = "\uE90F" },
            new ShellRoute { Id = "settings", Title = "Settings", Glyph = "\uE713" },
        ]);

        TabsRoute = Routes[0];
        PitstopRoute = Routes[1];
        // TreasurerRoute = Routes[2];
        ReportsRoute = Routes[2];
        AdminRoute = Routes[3];
        SettingsRoute = Routes[4];

        NavigateToCommand = new RelayCommand<ShellRoute?>(NavigateTo);
        SignOutCommand = new RelayCommand(SignOut);

        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IUserSessionService.DisplayName)
            or nameof(IUserSessionService.Role)
            or nameof(IUserSessionService.IsSignedIn)
            or nameof(IUserSessionService.IsAdmin)
            // or nameof(IUserSessionService.IsTreasurer)
            or nameof(IUserSessionService.CanAccessAdmin)
            or nameof(IUserSessionService.CanAccessReports))
            // or nameof(IUserSessionService.CanAccessTreasurer))
        {
            OnPropertyChanged(nameof(CanOpenAdminRoute));
            OnPropertyChanged(nameof(CanOpenReportsRoute));
            OnPropertyChanged(nameof(SignedInUserText));
            OnPropertyChanged(nameof(SignedInUserToolTip));
            OnPropertyChanged(nameof(ShowSignedInUser));
            // OnPropertyChanged(nameof(CanOpenTreasurerRoute));
        }
    }

    /// <summary>Admin sees the Admin bottom-nav button.</summary>
    public bool CanOpenAdminRoute => _session.CanAccessAdmin;

    /// <summary>Admin sees the Reports bottom-nav button.</summary>
    public bool CanOpenReportsRoute => _session.CanAccessReports;

    // /// <summary>Treasurer bottom-nav disabled.</summary>
    // public bool CanOpenTreasurerRoute => _session.CanAccessTreasurer;

    public ObservableCollection<ShellRoute> Routes { get; }

    public ShellRoute TabsRoute { get; }

    public ShellRoute PitstopRoute { get; }

    // public ShellRoute TreasurerRoute { get; }

    public ShellRoute ReportsRoute { get; }

    public ShellRoute AdminRoute { get; }

    public ShellRoute SettingsRoute { get; }

    public string HeaderTitle
    {
        get => _headerTitle;
        private set => SetProperty(ref _headerTitle, value);
    }

    public ShellRoute? SelectedRoute
    {
        get => _selectedRoute;
        private set => SetProperty(ref _selectedRoute, value);
    }

    public ICommand NavigateToCommand { get; }

    public IRelayCommand SignOutCommand { get; }

    public string FooterDateText
    {
        get => _footerDateText;
        private set => SetProperty(ref _footerDateText, value);
    }

    public string FooterTimeText
    {
        get => _footerTimeText;
        private set => SetProperty(ref _footerTimeText, value);
    }

    public string SquareStatusText
    {
        get => _squareStatusText;
        private set => SetProperty(ref _squareStatusText, value);
    }

    public bool SquareIsOnline
    {
        get => _squareIsOnline;
        private set => SetProperty(ref _squareIsOnline, value);
    }

    public bool ShowSignedInUser => _session.IsSignedIn && !string.IsNullOrWhiteSpace(SignedInUserText);

    public string SignedInUserText
    {
        get
        {
            var name = _session.DisplayName?.Trim();
            return string.IsNullOrWhiteSpace(name) ? string.Empty : name;
        }
    }

    public string SignedInUserToolTip
    {
        get
        {
            var role = _session.Role?.Trim();
            return string.IsNullOrWhiteSpace(role)
                ? "Signed in"
                : $"Signed in · {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(role)}";
        }
    }

    public void InitializeShell(Frame shellFrame)
    {
        _navigation.AttachShellFrame(shellFrame);
        StartFooterTimers(shellFrame.DispatcherQueue);
        OnPropertyChanged(nameof(SignedInUserText));
        OnPropertyChanged(nameof(SignedInUserToolTip));
        OnPropertyChanged(nameof(ShowSignedInUser));

        // Everyone starts on Tabs; nothing in that route is restricted.
        NavigateToRoute(TabsRoute);
    }

    private void StartFooterTimers(DispatcherQueue queue)
    {
        RefreshFooterClock();

        _footerClockTimer ??= queue.CreateTimer();
        _footerClockTimer.Interval = TimeSpan.FromSeconds(1);
        _footerClockTimer.Tick -= OnFooterClockTick;
        _footerClockTimer.Tick += OnFooterClockTick;
        if (!_footerClockTimer.IsRunning)
        {
            _footerClockTimer.Start();
        }

        _squareStatusTimer ??= queue.CreateTimer();
        _squareStatusTimer.Interval = TimeSpan.FromSeconds(30);
        _squareStatusTimer.Tick -= OnSquareStatusTick;
        _squareStatusTimer.Tick += OnSquareStatusTick;
        if (!_squareStatusTimer.IsRunning)
        {
            _squareStatusTimer.Start();
        }

        _ = RefreshSquareStatusAsync();
    }

    private void OnFooterClockTick(DispatcherQueueTimer sender, object args) => RefreshFooterClock();

    private void OnSquareStatusTick(DispatcherQueueTimer sender, object args) => _ = RefreshSquareStatusAsync();

    private void RefreshFooterClock()
    {
        var now = DateTime.Now;
        FooterDateText = now.ToString("d MMMM yyyy", CultureInfo.CurrentCulture);
        FooterTimeText = now.ToString("h:mm tt", CultureInfo.CurrentCulture);
    }

    private async Task RefreshSquareStatusAsync()
    {
        var (label, online) = await TabsSquareStatusHelper.LoadFooterStatusAsync(_squareConfig).ConfigureAwait(true);
        SquareStatusText = label;
        SquareIsOnline = online;
    }

    private void SignOut() => _signOut.SignOut();

    private void NavigateTo(ShellRoute? route)
    {
        if (route is null)
        {
            return;
        }

        NavigateToRoute(route);
    }

    private void NavigateToRoute(ShellRoute route)
    {
        // Defence-in-depth: bottom-nav visibility hides these buttons but we also
        // refuse to navigate via code paths or stale bindings.
        if (string.Equals(route.Id, "admin", System.StringComparison.OrdinalIgnoreCase)
            && !_session.CanAccessAdmin)
        {
            route = Routes[0];
        }
        else if (string.Equals(route.Id, "reports", System.StringComparison.OrdinalIgnoreCase)
            && !_session.CanAccessReports)
        {
            route = Routes[0];
        }
        // else if (string.Equals(route.Id, "treasurer", System.StringComparison.OrdinalIgnoreCase)
        //     && !_session.CanAccessTreasurer)
        // {
        //     route = Routes[0];
        // }

        if (string.Equals(route.Id, "settings", System.StringComparison.OrdinalIgnoreCase))
        {
            _navigation.Navigate(typeof(SettingsPage));
        }
        else if (string.Equals(route.Id, "admin", System.StringComparison.OrdinalIgnoreCase))
        {
            _navigation.Navigate(typeof(AdminHomePage));
        }
        else if (string.Equals(route.Id, "reports", System.StringComparison.OrdinalIgnoreCase))
        {
            _navigation.Navigate(typeof(ReportsHomePage));
        }
        // else if (string.Equals(route.Id, "treasurer", System.StringComparison.OrdinalIgnoreCase))
        // {
        //     _navigation.Navigate(typeof(TreasurerHomePage));
        // }
        else if (!_navigation.Navigate(typeof(WorkspacePage), route))
        {
            _workspace.ApplyRoute(route);
        }

        SelectedRoute = route;
        var who = string.IsNullOrWhiteSpace(_session.DisplayName) ? null : _session.DisplayName.Trim();
        HeaderTitle = who is null ? route.Title : $"{route.Title} · {who}";
    }
}
