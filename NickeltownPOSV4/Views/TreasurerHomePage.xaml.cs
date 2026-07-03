using System.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Treasurer;
using Windows.Storage.Pickers;

namespace NickeltownPOSV4.Views;

public sealed partial class TreasurerHomePage : Page
{
    private readonly IUserSessionService _session = App.Services.GetRequiredService<IUserSessionService>();
    private readonly IReportPathProvider _paths = App.Services.GetRequiredService<IReportPathProvider>();
    private readonly IExportedFileLauncher _launcher = App.Services.GetRequiredService<IExportedFileLauncher>();

    public TreasurerHomePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        TreasurerDataPaths.TryImportFromLegacyPosBarData();
        RefreshAll();
    }

    private void TreasurerTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshAll();

    private void RefreshAll()
    {
        TreasurerService.RefreshComputedBalances();
        LoadDashboard();
        LoadTransactions();
        LoadUncategorised();
        LoadAccounts();
        LoadCategories();
        LoadAudit();
        LoadReconciliation();
    }

    private void LoadDashboard()
    {
        DashboardPanel.Children.Clear();
        var accounts = TreasurerService.GetAccounts().Where(a => a.IsActive).ToList();
        var tx = TreasurerService.GetTransactions();
        var income = tx.Where(t => !t.IsVoided && t.Type == (int)TreasurerTransactionType.Income).Sum(t => t.Amount);
        var expense = tx.Where(t => !t.IsVoided && t.Type == (int)TreasurerTransactionType.Expense).Sum(t => t.Amount);
        AddDashboardLine($"Active accounts: {accounts.Count}");
        AddDashboardLine($"Transactions: {tx.Count(t => !t.IsVoided)}");
        AddDashboardLine($"Income total: ${income:0.00}");
        AddDashboardLine($"Expense total: ${expense:0.00}");
        AddDashboardLine($"Net: ${(income - expense):0.00}");
        foreach (var a in accounts.OrderBy(x => x.SortOrder).ThenBy(x => x.Name))
        {
            var bal = TreasurerService.GetAccountBalance(a.Id);
            AddDashboardLine($"{a.Name}: ${bal:0.00}");
        }
    }

    private void AddDashboardLine(string text) =>
        DashboardPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 14,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["PosTextPrimaryBrush"],
        });

    private void LoadTransactions()
    {
        var rows = TreasurerService.GetTransactions()
            .Where(t => !t.IsVoided)
            .Take(500)
            .Select(t => $"{t.Date:dd-MMM-yyyy}  {TypeLabel(t.Type)}  ${t.Amount:0.00}  {t.Description}")
            .ToList();
        TransactionsList.ItemsSource = rows;
    }

    private void LoadUncategorised()
    {
        var cats = TreasurerService.GetCategories().ToDictionary(c => c.Id, c => c.Name);
        var rows = TreasurerService.GetTransactions()
            .Where(t => !t.IsVoided && t.CategoryId is null && t.Type != (int)TreasurerTransactionType.Transfer)
            .Select(t => $"{t.Date:dd-MMM-yyyy}  ${t.Amount:0.00}  {t.Description}")
            .ToList();
        UncategorisedList.ItemsSource = rows;
    }

    private void LoadAccounts()
    {
        AccountsList.ItemsSource = TreasurerService.GetAccounts()
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .Select(a => $"{a.Name}  ·  ${TreasurerService.GetAccountBalance(a.Id):0.00}  {(a.IsActive ? "" : "(inactive)")}")
            .ToList();
    }

    private void LoadCategories()
    {
        CategoriesList.ItemsSource = TreasurerService.GetCategories()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => $"{c.Name}  ({(c.Type == (int)TreasurerCategoryType.Income ? "Income" : "Expense")})")
            .ToList();
    }

    private void LoadAudit()
    {
        AuditList.ItemsSource = TreasurerService.GetAuditEntries()
            .OrderByDescending(a => a.Timestamp)
            .Take(300)
            .Select(a => $"{a.Timestamp:dd-MMM-yyyy HH:mm}  {a.UserName}  {a.Action}  {a.EntityType}")
            .ToList();
    }

    private void LoadReconciliation()
    {
        ReconciliationList.ItemsSource = TreasurerService.GetBankTransactions()
            .OrderByDescending(b => b.TransactionDate)
            .Take(400)
            .Select(b => $"{b.TransactionDate:dd-MMM-yyyy}  ${b.Amount:0.00}  {b.Description}  [{MatchLabel(b.MatchStatus)}]")
            .ToList();
    }

    private static string TypeLabel(int type) => type switch
    {
        (int)TreasurerTransactionType.Income => "Income",
        (int)TreasurerTransactionType.Expense => "Expense",
        (int)TreasurerTransactionType.Transfer => "Transfer",
        (int)TreasurerTransactionType.Adjustment => "Adjustment",
        _ => "Other",
    };

    private static string MatchLabel(int status) => status switch
    {
        (int)BankTransactionMatchStatus.Reconciled => "Reconciled",
        (int)BankTransactionMatchStatus.ManuallyMatched => "Matched",
        (int)BankTransactionMatchStatus.AutoSuggested => "Suggested",
        (int)BankTransactionMatchStatus.Ignored => "Ignored",
        _ => "Unmatched",
    };

    private async void AddTransaction_Click(object sender, RoutedEventArgs e)
    {
        var amountBox = new TextBox { PlaceholderText = "Amount" };
        var descBox = new TextBox { PlaceholderText = "Description" };
        var typeBox = new ComboBox
        {
            ItemsSource = new[] { "Expense", "Income", "Transfer", "Adjustment" },
            SelectedIndex = 0,
        };
        var panel = new StackPanel { Spacing = 8, MinWidth = 320 };
        panel.Children.Add(typeBox);
        panel.Children.Add(amountBox);
        panel.Children.Add(descBox);
        if (await ShowDialogAsync("New transaction", panel) != ContentDialogResult.Primary)
        {
            return;
        }

        if (!decimal.TryParse(amountBox.Text, out var amount) || amount <= 0)
        {
            return;
        }

        var accounts = TreasurerService.GetAccounts().Where(a => a.IsActive).ToList();
        if (accounts.Count == 0)
        {
            return;
        }

        var type = typeBox.SelectedIndex switch
        {
            1 => (int)TreasurerTransactionType.Income,
            2 => (int)TreasurerTransactionType.Transfer,
            3 => (int)TreasurerTransactionType.Adjustment,
            _ => (int)TreasurerTransactionType.Expense,
        };
        var tx = new TreasurerTransaction
        {
            Date = DateTime.Today,
            Type = type,
            Amount = amount,
            Description = descBox.Text?.Trim() ?? string.Empty,
            AccountId = accounts[0].Id,
        };
        TreasurerService.AddTransaction(tx, _session.DisplayName ?? "Treasurer");
        RefreshAll();
    }

    private void RefreshTransactions_Click(object sender, RoutedEventArgs e) => LoadTransactions();

    private void TransactionsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => LoadTransactions();

    private async void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { PlaceholderText = "Account name" };
        if (await ShowDialogAsync("New account", nameBox) != ContentDialogResult.Primary
            || string.IsNullOrWhiteSpace(nameBox.Text))
        {
            return;
        }

        var list = TreasurerService.GetAccounts();
        list.Add(new TreasurerAccount
        {
            Name = nameBox.Text.Trim(),
            OpeningBalance = 0,
            CurrentBalance = 0,
            IsActive = true,
            SortOrder = list.Count,
        });
        TreasurerService.SaveAccounts(list);
        RefreshAll();
    }

    private void AccountsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => LoadAccounts();

    private async void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { PlaceholderText = "Category name" };
        var typeBox = new ComboBox { ItemsSource = new[] { "Expense", "Income" }, SelectedIndex = 0 };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(nameBox);
        panel.Children.Add(typeBox);
        if (await ShowDialogAsync("New category", panel) != ContentDialogResult.Primary
            || string.IsNullOrWhiteSpace(nameBox.Text))
        {
            return;
        }

        var list = TreasurerService.GetCategories();
        list.Add(new TreasurerCategory
        {
            Name = nameBox.Text.Trim(),
            Type = typeBox.SelectedIndex == 1 ? (int)TreasurerCategoryType.Income : (int)TreasurerCategoryType.Expense,
            IsArchived = false,
            SortOrder = list.Count,
        });
        TreasurerService.SaveCategories(list);
        RefreshAll();
    }

    private async void ExportTreasurerReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = TreasurerReportGenerator.BuildSummaryPdf();
            var dir = _paths.GetRoot();
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"Treasurer_Report_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            ReportsStatusText.Text = _launcher.TryLaunch(path)
                ? $"Saved and opened: {path}"
                : $"Saved: {path}";
        }
        catch (Exception ex)
        {
            ReportsStatusText.Text = ex.Message;
        }
    }

    private async void ImportBankCsv_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = App.Services.GetRequiredService<IWindowHandleProvider>().WindowHandle;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".csv");
        picker.FileTypeFilter.Add(".txt");
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var accounts = TreasurerService.GetAccounts().Where(a => a.IsActive).ToList();
        if (accounts.Count == 0)
        {
            return;
        }

        try
        {
            var parsed = BankStatementImporter.ParseCsvWithPreview(file.Path);
            await Task.Run(() =>
                BankStatementImporter.ImportToBankTransactions(
                    parsed.Rows,
                    accounts[0].Id,
                    file.Name,
                    _session.DisplayName ?? "Treasurer"));
            RefreshAll();
        }
        catch (Exception ex)
        {
            ReportsStatusText.Text = ex.Message;
        }
    }

    private void RefreshReconciliation_Click(object sender, RoutedEventArgs e) => LoadReconciliation();

    private static async Task<ContentDialogResult> ShowDialogAsync(string title, UIElement content)
    {
        var dlg = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.Services.GetRequiredService<IWindowHandleProvider>().GetXamlRoot(),
        };
        return await dlg.ShowAsync();
    }
}
