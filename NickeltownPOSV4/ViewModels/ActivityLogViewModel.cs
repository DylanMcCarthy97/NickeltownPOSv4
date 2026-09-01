using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels;

public sealed class ActivityLogViewModel : ObservableViewModel
{
    private readonly IAuditLogService _audit;
    private readonly IUserSessionService _session;
    private readonly INavigationService _navigation;

    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public ActivityLogViewModel(
        IAuditLogService audit,
        IUserSessionService session,
        INavigationService navigation)
    {
        _audit = audit;
        _session = session;
        _navigation = navigation;
        Rows = new PagedCollection<ActivityLogRowViewModel>(pageSize: 8);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        BackCommand = new RelayCommand(() => _navigation.TryGoBack());
    }

    public PagedCollection<ActivityLogRowViewModel> Rows { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IRelayCommand BackCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task InitializeAsync()
    {
        if (!_session.CanAccessAdmin)
        {
            StatusMessage = "Admin access required.";
            _navigation.TryGoBack();
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
    }

    public async Task RefreshAsync()
    {
        if (!_session.CanAccessAdmin)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading activity...";
            var nowLocal = DateTimeOffset.Now;
            var data = await _audit.GetRecentAsync(maxEntries: 500, staffFacingOnly: true).ConfigureAwait(true);
            Rows.Replace(data.Select(e => new ActivityLogRowViewModel(e, nowLocal)));
            StatusMessage = Rows.TotalCount == 0
                ? "No activity recorded yet. Undos, balance changes, stock edits, and voids will show up here."
                : $"{Rows.TotalCount} recent change(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class ActivityLogRowViewModel
{
    public ActivityLogRowViewModel(AuditLogEntry entry, DateTimeOffset nowLocal)
    {
        SummaryText = ActivityLogText.FormatLine(entry, nowLocal);
        TimeText = ActivityLogText.FormatTime(entry.OccurredAt, nowLocal);
        StaffText = string.IsNullOrWhiteSpace(entry.StaffName) ? "Unknown" : entry.StaffName.Trim();
        ActionText = ActivityLogText.FormatAction(entry);
        Success = entry.Success;
        AmountText = entry.Amount is { } amt && amt != 0m
            ? amt.ToString("C2", CultureInfo.CurrentCulture)
            : string.Empty;
    }

    public string SummaryText { get; }

    public string TimeText { get; }

    public string StaffText { get; }

    public string ActionText { get; }

    public string AmountText { get; }

    public bool Success { get; }
}
