using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class ArchivedTabsViewModel : SettingsSubViewModelBase
{
    private readonly IReportExportService _reports;

    public ArchivedTabsViewModel(INavigationService navigation, IReportExportService reports)
        : base(navigation)
    {
        _reports = reports;
        Rows = new PagedCollection<ArchivedTabRowViewModel>(pageSize: 7);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public PagedCollection<ArchivedTabRowViewModel> Rows { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            SetStatus("Loading archived tabs...");
            var data = await _reports.ListArchivedTabsAsync().ConfigureAwait(true);
            Rows.Replace(data.Select(r => new ArchivedTabRowViewModel(r)));
            SetStatus(Rows.TotalCount == 0 ? "No archived or closed tabs found." : $"{Rows.TotalCount} archived/closed tabs.");
        }
        catch (Exception ex)
        {
            SetStatus($"Load failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class ArchivedTabRowViewModel : ObservableViewModel
{
    public ArchivedTabRowViewModel(ArchivedTabListRow row)
    {
        Id = row.Id;
        DisplayName = row.DisplayName;
        TabType = row.TabType;
        BalanceText = row.Balance.ToString("C2", CultureInfo.CurrentCulture);
        var lastActivity = row.ClosedAt ?? row.LastActivityAt;
        LastActivityText = lastActivity is null
            ? "No activity recorded"
            : lastActivity.Value.ToString("yyyy-MM-dd HH:mm");
        StatusText = (row.IsClosed, row.IsArchived) switch
        {
            (true, true) => "Closed · archived",
            (true, false) => "Closed",
            (false, true) => "Archived",
            _ => "Open",
        };
    }

    public long Id { get; }

    public string DisplayName { get; }

    public string TabType { get; }

    public string BalanceText { get; }

    public string LastActivityText { get; }

    public string StatusText { get; }
}
