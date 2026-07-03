using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Themes;
using NickeltownPOSV4.Views;
// MEMBERSHIP MODULE DISABLED - re-enable in ServiceCollection/App/AdminHome
// using NickeltownPOSV4.Views.Membership;
using NickeltownPOSV4.Views.Settings;

namespace NickeltownPOSV4.ViewModels;

/// <summary>
/// Settings page: general actions (logout, exit) plus admin tile commands when shown on <see cref="AdminHomePage"/>.
/// Monthly/stock PDF exports live under the Reports bottom tab.
/// </summary>
public sealed class SettingsPageViewModel : ObservableViewModel
{
    private readonly IUserSessionService _session;
    private readonly INavigationService _navigation;
    private readonly IRootNavigationCoordinator _rootNav;
    private readonly ISlidePanelService _slidePanel;
    private readonly ISerialCashDrawerService _cashDrawer;

    private readonly IPosThemeService _themes;

    private readonly Data.Sqlite.SqliteConnectionFactory _db;

    private readonly ISquareRecoveryRepository _squareRecovery;

    private readonly IPitstopPaymentRecoveryService _paymentRecovery;

    private Func<XamlRoot?>? _xamlRootProvider;

    private bool _isAdmin;

    private bool _paymentRecoveryRequired;

    private string _paymentRecoveryAmountText = string.Empty;

    private string _paymentRecoveryWhenText = string.Empty;

    private string _paymentRecoveryTransactionId = string.Empty;

    private long _paymentRecoveryAttemptId;

    public SettingsPageViewModel(
        IUserSessionService session,
        INavigationService navigation,
        IRootNavigationCoordinator rootNav,
        ISlidePanelService slidePanel,
        ISerialCashDrawerService cashDrawer,
        IPosThemeService themes,
        Data.Sqlite.SqliteConnectionFactory db,
        ISquareRecoveryRepository squareRecovery,
        IPitstopPaymentRecoveryService paymentRecovery)
    {
        _session = session;
        _navigation = navigation;
        _rootNav = rootNav;
        _slidePanel = slidePanel;
        _cashDrawer = cashDrawer;
        _themes = themes;
        _db = db;
        _squareRecovery = squareRecovery;
        _paymentRecovery = paymentRecovery;

        _isAdmin = _session.IsManager;
        _session.PropertyChanged += OnSessionPropertyChanged;

        BackupNowCommand = new RelayCommand(() => NavigateAdmin(typeof(BackupPage)));
        ViewArchivedCommand = new RelayCommand(() => NavigateAdmin(typeof(ArchivedTabsPage)));
        ViewPreviousPitstopsCommand = new RelayCommand(() => NavigateAdmin(typeof(PreviousPitstopsPage)));
        SquareRecoveryCommand = new RelayCommand(() => NavigateAdmin(typeof(SquareRecoveryPage)));
        SystemCheckCommand = new RelayCommand(() => NavigateAdmin(typeof(SystemCheckPage)));
        KickCashDrawerCommand = new AsyncRelayCommand(KickCashDrawerAsync);
        StockManagementCommand = new RelayCommand(OpenStockManagement);
        EmailConfigCommand = new RelayCommand(() => NavigateAdmin(typeof(EmailConfigPage)));
        ComPortConfigCommand = new RelayCommand(() => NavigateAdmin(typeof(ComPortConfigPage)));
        SquareSettingsCommand = new RelayCommand(() => NavigateAdmin(typeof(SquareConfigPage)));
        DataImportExportCommand = new RelayCommand(OpenDataImportExport);
        UserManagementCommand = new RelayCommand(() => NavigateAdmin(typeof(UserManagementPage)));
        AppUpdatesCommand = new RelayCommand(() => NavigateAdmin(typeof(UpdateConfigPage)));
        // MEMBERSHIP MODULE DISABLED - re-enable in ServiceCollection/App/AdminHome
        // MembershipCommand = new RelayCommand(() => NavigateAdmin(typeof(MembershipHomePage)));

        AppearanceCommand = new AsyncRelayCommand(OpenAppearancePickerAsync);

        RecoverPaymentCommand = new AsyncRelayCommand(RecoverPrimaryPaymentAsync, () => _paymentRecoveryRequired);
        IgnorePaymentRecoveryCommand = new AsyncRelayCommand(IgnorePrimaryPaymentAsync, () => _paymentRecoveryRequired);
        ViewPaymentRecoveryCommand = new RelayCommand(() => NavigateAdmin(typeof(SquareRecoveryPage)));

        LogoutCommand = new RelayCommand(Logout);
        ExitCommand = new RelayCommand(ExitApp);
    }

    public bool IsAdmin
    {
        get => _isAdmin;
        private set => SetProperty(ref _isAdmin, value);
    }

    public IRelayCommand BackupNowCommand { get; }

    public IRelayCommand ViewArchivedCommand { get; }

    public IRelayCommand ViewPreviousPitstopsCommand { get; }

    public IRelayCommand SquareRecoveryCommand { get; }

    public IRelayCommand SystemCheckCommand { get; }

    public IAsyncRelayCommand KickCashDrawerCommand { get; }

    public IRelayCommand StockManagementCommand { get; }

    public IRelayCommand EmailConfigCommand { get; }

    public IRelayCommand ComPortConfigCommand { get; }

    public IRelayCommand SquareSettingsCommand { get; }

    public IRelayCommand DataImportExportCommand { get; }

    public IRelayCommand UserManagementCommand { get; }

    public IRelayCommand AppUpdatesCommand { get; }

    // MEMBERSHIP MODULE DISABLED - re-enable in ServiceCollection/App/AdminHome
    // public IRelayCommand MembershipCommand { get; }

    public IAsyncRelayCommand AppearanceCommand { get; }

    public IRelayCommand LogoutCommand { get; }

    public IRelayCommand ExitCommand { get; }

    public IAsyncRelayCommand RecoverPaymentCommand { get; }

    public IAsyncRelayCommand IgnorePaymentRecoveryCommand { get; }

    public IRelayCommand ViewPaymentRecoveryCommand { get; }

    public bool PaymentRecoveryRequired
    {
        get => _paymentRecoveryRequired;
        private set
        {
            if (SetProperty(ref _paymentRecoveryRequired, value))
            {
                RecoverPaymentCommand.NotifyCanExecuteChanged();
                IgnorePaymentRecoveryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string PaymentRecoveryAmountText => _paymentRecoveryAmountText;

    public string PaymentRecoveryWhenText => _paymentRecoveryWhenText;

    public string PaymentRecoveryTransactionId => _paymentRecoveryTransactionId;

    public void AttachXamlRoot(Func<XamlRoot?> provider) => _xamlRootProvider = provider;

    public void RefreshFromSession() => IsAdmin = _session.IsManager;

    public async Task RefreshPaymentRecoveryAlertAsync()
    {
        if (!_session.IsManager)
        {
            PaymentRecoveryRequired = false;
            return;
        }

        var alert = await _squareRecovery.GetPrimaryRecoveryAlertAsync().ConfigureAwait(true);
        if (alert is null)
        {
            PaymentRecoveryRequired = false;
            OnPropertyChanged(nameof(PaymentRecoveryAmountText));
            OnPropertyChanged(nameof(PaymentRecoveryWhenText));
            OnPropertyChanged(nameof(PaymentRecoveryTransactionId));
            return;
        }

        _paymentRecoveryAttemptId = alert.AttemptId;
        _paymentRecoveryAmountText = alert.ChargedAmount.ToString("C2", System.Globalization.CultureInfo.CurrentCulture);
        _paymentRecoveryWhenText = alert.OccurredAt.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
        _paymentRecoveryTransactionId = alert.TransactionId;
        PaymentRecoveryRequired = true;
        OnPropertyChanged(nameof(PaymentRecoveryAmountText));
        OnPropertyChanged(nameof(PaymentRecoveryWhenText));
        OnPropertyChanged(nameof(PaymentRecoveryTransactionId));
    }

    private async Task RecoverPrimaryPaymentAsync()
    {
        if (_paymentRecoveryAttemptId <= 0)
        {
            return;
        }

        var result = await _paymentRecovery.RecoverPitstopSaleAsync(_paymentRecoveryAttemptId).ConfigureAwait(true);
        await ShowInfoAsync(
            result.Ok ? "Payment recovered" : "Recovery failed",
            result.Ok
                ? "The Pitstop sale was saved and stock was deducted."
                : result.ErrorMessage ?? "Could not recover the sale.").ConfigureAwait(true);
        await RefreshPaymentRecoveryAlertAsync().ConfigureAwait(true);
    }

    private async Task IgnorePrimaryPaymentAsync()
    {
        if (_paymentRecoveryAttemptId <= 0)
        {
            return;
        }

        var result = await _paymentRecovery.IgnoreAsync(_paymentRecoveryAttemptId, "Ignored from Admin dashboard").ConfigureAwait(true);
        if (!result.Ok)
        {
            await ShowInfoAsync("Ignore failed", result.ErrorMessage ?? "Could not ignore.").ConfigureAwait(true);
            return;
        }

        await RefreshPaymentRecoveryAlertAsync().ConfigureAwait(true);
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IUserSessionService.Role)
            or nameof(IUserSessionService.IsAdmin)
            or nameof(IUserSessionService.IsTreasurer)
            or nameof(IUserSessionService.IsManager))
        {
            IsAdmin = _session.IsManager;
        }
    }

    private void NavigateAdmin(Type pageType)
    {
        _slidePanel.Close();
        _navigation.Navigate(pageType);
    }

    private async Task KickCashDrawerAsync()
    {
        try
        {
            await _cashDrawer.KickAsync().ConfigureAwait(true);
            await ShowInfoAsync("Cash drawer", "Drawer-kick pulse sent on the configured COM port.").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await ShowInfoAsync("Cash drawer error", ex.Message).ConfigureAwait(true);
        }
    }

    private void OpenStockManagement()
    {
        _slidePanel.Close();
        _navigation.Navigate(typeof(StockManagementPage));
    }

    private void OpenDataImportExport()
    {
        _slidePanel.Close();
        _navigation.Navigate(
            typeof(MigrationPage),
            new ShellRoute { Id = "settings", Title = "Settings", Glyph = "\uE713" });
    }

    private void Logout()
    {
        _slidePanel.Close();
        _session.Clear();
        _rootNav.NavigateToLogin();
    }

    private void ExitApp() => Application.Current.Exit();

    private async Task OpenAppearancePickerAsync()
    {
        var xamlRoot = _xamlRootProvider?.Invoke();
        if (xamlRoot is null)
        {
            return;
        }

        var originalTheme = _themes.CurrentThemeId;
        var selectedTheme = originalTheme;

        var previewCanvas = new Border
        {
            Width = 200,
            Height = 120,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
        };
        var previewCard = new Border
        {
            Width = 120,
            Height = 56,
            Margin = new Thickness(16, 20, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(8),
        };
        var previewAccent = new Border
        {
            Width = 64,
            Height = 28,
            Margin = new Thickness(0, 0, 16, 16),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            CornerRadius = new CornerRadius(6),
        };
        var previewText = new TextBlock
        {
            Margin = new Thickness(16, 0, 0, 12),
            VerticalAlignment = VerticalAlignment.Bottom,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = "Preview",
        };
        var previewRoot = new Grid();
        previewRoot.Children.Add(previewCanvas);
        previewRoot.Children.Add(previewCard);
        previewRoot.Children.Add(previewAccent);
        previewRoot.Children.Add(previewText);

        void RefreshPreview(UiThemeId id)
        {
            AppTheme.Apply(id);
            _themes.PushBrushesToApplicationResources();
            previewCanvas.Background = new SolidColorBrush(AppTheme.Background);
            previewCanvas.BorderBrush = new SolidColorBrush(AppTheme.Border);
            previewCard.Background = new SolidColorBrush(AppTheme.Card);
            previewAccent.Background = new SolidColorBrush(AppTheme.Accent);
            previewText.Foreground = new SolidColorBrush(AppTheme.TextPrimary);
        }

        RefreshPreview(selectedTheme);

        ListView BuildThemeList(IReadOnlyList<UiThemeId> ids, bool fixedHeight)
        {
            var items = ids.Select(id => new ThemeListItem(id, _themes.GetDisplayName(id))).ToList();
            var themeList = new ListView
            {
                Width = 220,
                SelectionMode = ListViewSelectionMode.Single,
                ItemsSource = items,
                DisplayMemberPath = nameof(ThemeListItem.Label),
            };
            if (fixedHeight)
            {
                themeList.Height = Math.Max(120, items.Count * 40 + 4);
            }

            themeList.SelectedItem = items.FirstOrDefault(i => i.Id == selectedTheme);
            return themeList;
        }

        var recommendedHeader = new TextBlock
        {
            Text = "Recommended for POS",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Application.Current.Resources["PosTextPrimaryBrush"] as Brush,
            Margin = new Thickness(0, 0, 0, 4),
        };
        var recommendedList = BuildThemeList(_themes.RecommendedThemeIds, fixedHeight: true);

        var moreItems = _themes.MoreThemeIds
            .Select(id => new ThemeListItem(id, _themes.GetDisplayName(id)))
            .ToList();
        var moreCombo = new ComboBox
        {
            Width = 220,
            Margin = new Thickness(0, 12, 0, 0),
            Header = "More themes",
            PlaceholderText = "Choose from additional themes…",
            ItemsSource = moreItems,
            DisplayMemberPath = nameof(ThemeListItem.Label),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        if (moreItems.Any(i => i.Id == selectedTheme))
        {
            moreCombo.SelectedItem = moreItems.First(i => i.Id == selectedTheme);
            recommendedList.SelectedItem = null;
        }

        recommendedList.SelectionChanged += (_, _) =>
        {
            if (recommendedList.SelectedItem is ThemeListItem item)
            {
                selectedTheme = item.Id;
                RefreshPreview(selectedTheme);
                if (moreCombo.SelectedItem is not null)
                {
                    moreCombo.SelectedItem = null;
                }
            }
        };

        moreCombo.SelectionChanged += (_, _) =>
        {
            if (moreCombo.SelectedItem is ThemeListItem item)
            {
                selectedTheme = item.Id;
                RefreshPreview(selectedTheme);
                recommendedList.SelectedItem = null;
            }
        };

        var listColumn = new StackPanel { Width = 220, Spacing = 0 };
        listColumn.Children.Add(recommendedHeader);
        listColumn.Children.Add(recommendedList);
        listColumn.Children.Add(moreCombo);

        var titleBlock = new TextBlock
        {
            Text = "Appearances",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Application.Current.Resources["PosTextPrimaryBrush"] as Brush,
            Margin = new Thickness(0, 0, 0, 6),
        };

        var layout = new Grid { MinWidth = 460, MinHeight = 340 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid.SetRow(titleBlock, 0);
        Grid.SetColumnSpan(titleBlock, 2);
        layout.Children.Add(titleBlock);

        var hint = new TextBlock
        {
            Text = "Select a theme to preview it live. Apply saves to your user profile.",
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = Application.Current.Resources["PosTextSecondaryBrush"] as Brush,
            Margin = new Thickness(0, 0, 0, 10),
        };
        Grid.SetRow(hint, 1);
        Grid.SetColumnSpan(hint, 2);
        layout.Children.Add(hint);

        Grid.SetRow(listColumn, 2);
        Grid.SetRow(previewRoot, 2);
        Grid.SetColumn(previewRoot, 1);
        layout.Children.Add(listColumn);
        layout.Children.Add(previewRoot);

        var dlg = PosContentDialogHelper.Create(
            xamlRoot,
            string.Empty,
            layout,
            primaryButtonText: "Apply",
            closeButtonText: "Cancel",
            defaultButton: ContentDialogButton.Primary);

        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            _themes.Apply(originalTheme);
            _themes.PushBrushesToApplicationResources();
            return;
        }

        _themes.Apply(selectedTheme);
        if (_session.ActiveStaffId is long pk)
        {
            using var conn = _db.OpenConnection();
            Dapper.SqlMapper.Execute(
                conn,
                "UPDATE Bartenders SET UiTheme = @t WHERE Id = @id",
                new { t = selectedTheme.ToString(), id = pk });
        }
    }

    private sealed record ThemeListItem(UiThemeId Id, string Label);

    private async Task ShowInfoAsync(string title, string body)
    {
        var xamlRoot = _xamlRootProvider?.Invoke();
        if (xamlRoot is null)
        {
            return;
        }

        var dialog = PosContentDialogHelper.Create(
            xamlRoot,
            title,
            new TextBlock
            {
                Text = body,
                TextWrapping = TextWrapping.WrapWholeWords,
            },
            closeButtonText: "OK",
            defaultButton: ContentDialogButton.Close);

        await dialog.ShowAsync();
    }
}
