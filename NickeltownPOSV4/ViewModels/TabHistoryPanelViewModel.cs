using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels;

public enum TabHistoryRangePreset
{
    AllTime = 0,

    ThisMonth = 1,

    LastMonth = 2,

    Last90Days = 3,

    Custom = 4,
}

public sealed class TabHistoryPanelViewModel : ObservableViewModel
{
    private readonly ITabHistoryQuery _history;

    private readonly ITabHistorySession _session;

    private readonly ISlidePanelService _slide;

    private readonly IReportPathProvider _paths;

    private readonly IExportedFileLauncher _launcher;

    private string _header = "Tab history";

    private string _status = string.Empty;

    private string _rangeSummary = "All time";

    private TabHistoryRangePreset _preset = TabHistoryRangePreset.AllTime;

    private DateTimeOffset _customStart = DateTimeOffset.Now;

    private DateTimeOffset _customEnd = DateTimeOffset.Now;

    private bool _isExporting;

    public TabHistoryPanelViewModel(
        ITabHistoryQuery history,
        ITabHistorySession session,
        ISlidePanelService slide,
        IReportPathProvider paths,
        IExportedFileLauncher launcher)
    {
        _history = history;
        _session = session;
        _slide = slide;
        _paths = paths;
        _launcher = launcher;
        CloseCommand = new RelayCommand(Close);
        PresetAllTimeCommand = new RelayCommand(() => SetPreset(TabHistoryRangePreset.AllTime));
        PresetThisMonthCommand = new RelayCommand(() => SetPreset(TabHistoryRangePreset.ThisMonth));
        PresetLastMonthCommand = new RelayCommand(() => SetPreset(TabHistoryRangePreset.LastMonth));
        PresetLast90DaysCommand = new RelayCommand(() => SetPreset(TabHistoryRangePreset.Last90Days));
        PresetCustomCommand = new RelayCommand(() => SetPreset(TabHistoryRangePreset.Custom));
        ApplyCustomRangeCommand = new AsyncRelayCommand(ApplyCustomRangeAsync, CanApplyCustom);
        ExportPdfCommand = new AsyncRelayCommand(ExportSelectedPdfAsync, CanExportSelected);
        SelectAllCommand = new RelayCommand(SelectAllEntries);
        SelectNoneCommand = new RelayCommand(SelectNoEntries);
    }

    public ObservableCollection<TabHistoryMonthGroupViewModel> MonthGroups { get; } = new();

    public string Header
    {
        get => _header;
        private set => SetProperty(ref _header, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string RangeSummary
    {
        get => _rangeSummary;
        private set => SetProperty(ref _rangeSummary, value);
    }

    public TabHistoryRangePreset Preset
    {
        get => _preset;
        private set
        {
            if (SetProperty(ref _preset, value))
            {
                OnPropertyChanged(nameof(IsCustomPreset));
                OnPropertyChanged(nameof(PresetAllOpacity));
                OnPropertyChanged(nameof(PresetThisMonthOpacity));
                OnPropertyChanged(nameof(PresetLastMonthOpacity));
                OnPropertyChanged(nameof(PresetLast90Opacity));
                OnPropertyChanged(nameof(PresetCustomOpacity));
                ApplyCustomRangeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsCustomPreset => _preset == TabHistoryRangePreset.Custom;

    public DateTimeOffset CustomStart
    {
        get => _customStart;
        set
        {
            if (SetProperty(ref _customStart, value))
            {
                ApplyCustomRangeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public DateTimeOffset CustomEnd
    {
        get => _customEnd;
        set
        {
            if (SetProperty(ref _customEnd, value))
            {
                ApplyCustomRangeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
                ExportPdfCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public double PresetAllOpacity => _preset == TabHistoryRangePreset.AllTime ? 1.0 : 0.78;

    public double PresetThisMonthOpacity => _preset == TabHistoryRangePreset.ThisMonth ? 1.0 : 0.78;

    public double PresetLastMonthOpacity => _preset == TabHistoryRangePreset.LastMonth ? 1.0 : 0.78;

    public double PresetLast90Opacity => _preset == TabHistoryRangePreset.Last90Days ? 1.0 : 0.78;

    public double PresetCustomOpacity => _preset == TabHistoryRangePreset.Custom ? 1.0 : 0.78;

    public IRelayCommand CloseCommand { get; }

    public IRelayCommand PresetAllTimeCommand { get; }

    public IRelayCommand PresetThisMonthCommand { get; }

    public IRelayCommand PresetLastMonthCommand { get; }

    public IRelayCommand PresetLast90DaysCommand { get; }

    public IRelayCommand PresetCustomCommand { get; }

    public IAsyncRelayCommand ApplyCustomRangeCommand { get; }

    public IAsyncRelayCommand ExportPdfCommand { get; }

    public IRelayCommand SelectAllCommand { get; }

    public IRelayCommand SelectNoneCommand { get; }

    public async Task LoadAsync()
    {
        MonthGroups.Clear();
        Status = string.Empty;
        ExportPdfCommand.NotifyCanExecuteChanged();

        if (string.IsNullOrWhiteSpace(_session.TabLegacyId))
        {
            Header = "Tab history";
            RangeSummary = "—";
            Status = "Select a tab on the board first, then open Tab history again.";
            return;
        }

        var name = string.IsNullOrWhiteSpace(_session.TabDisplayName)
            ? _session.TabLegacyId
            : _session.TabDisplayName;
        Header = $"History — {name}";

        await ReloadForCurrentPresetAsync().ConfigureAwait(true);
    }

    private void SetPreset(TabHistoryRangePreset preset)
    {
        if (preset == TabHistoryRangePreset.Custom)
        {
            var now = DateTimeOffset.Now;
            var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local);
            CustomStart = new DateTimeOffset(first);
            CustomEnd = now;
        }

        Preset = preset;
        if (preset != TabHistoryRangePreset.Custom)
        {
            _ = ReloadForCurrentPresetAsync();
        }
    }

    private bool CanApplyCustom() =>
        _preset == TabHistoryRangePreset.Custom && CustomEnd.Date >= CustomStart.Date;

    private async Task ApplyCustomRangeAsync()
    {
        if (!CanApplyCustom())
        {
            Status = "Pick a start and end day (end on or after start), then tap Apply range.";
            return;
        }

        await ReloadForCurrentPresetAsync().ConfigureAwait(true);
    }

    private async Task ReloadForCurrentPresetAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.TabLegacyId))
        {
            return;
        }

        MonthGroups.Clear();
        Status = "Loading…";

        try
        {
            var legacy = _session.TabLegacyId!.Trim();
            IReadOnlyList<TabHistoryEntryRow> rows;

            if (_preset == TabHistoryRangePreset.AllTime)
            {
                RangeSummary = "All time";
                rows = await _history.GetTabEntriesAsync(legacy).ConfigureAwait(true);
            }
            else if (_preset == TabHistoryRangePreset.Custom)
            {
                if (!TryGetCustomUtcBounds(out var startUtc, out var endExUtc, out var label))
                {
                    Status = "Pick start and end dates for the custom range.";
                    return;
                }

                RangeSummary = label;
                rows = await _history.GetTabEntriesInRangeAsync(legacy, startUtc, endExUtc).ConfigureAwait(true);
            }
            else if (TryGetPresetUtcBounds(_preset, out var s, out var ex, out var cap))
            {
                RangeSummary = cap;
                rows = await _history.GetTabEntriesInRangeAsync(legacy, s, ex).ConfigureAwait(true);
            }
            else
            {
                RangeSummary = "All time";
                rows = await _history.GetTabEntriesAsync(legacy).ConfigureAwait(true);
            }

            ExportPdfCommand.NotifyCanExecuteChanged();

            if (rows.Count == 0)
            {
                Status = $"No lines in SQLite for this tab in “{RangeSummary}”.";
                return;
            }

            BuildMonthGroups(rows);
            var total = MonthGroups.Sum(g => g.Entries.Count);
            Status =
                $"{total} line(s) in {MonthGroups.Count} month group(s) — {RangeSummary}. " +
                "Check entries (or a month) and tap Export selected to PDF.";
        }
        catch (Exception ex)
        {
            Status = $"Could not load history: {ex.Message}";
        }
    }

    private void BuildMonthGroups(IReadOnlyList<TabHistoryEntryRow> rows)
    {
        var grouped = rows
            .Select(r => (Row: r, Line: TabHistoryEntryDisplayFormatter.ParseLedgerLine(r)))
            .OrderByDescending(x => x.Line.WhenLocal ?? DateTimeOffset.MinValue)
            .GroupBy(x => x.Line.MonthGroupLabel, StringComparer.OrdinalIgnoreCase);

        foreach (var grp in grouped)
        {
            var monthVm = new TabHistoryMonthGroupViewModel(grp.Key);
            monthVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TabHistoryMonthGroupViewModel.IsSelected))
                {
                    ExportPdfCommand.NotifyCanExecuteChanged();
                }
            };
            foreach (var item in grp)
            {
                var entryVm = new TabHistoryEntryItemViewModel(item.Row, item.Line);
                entryVm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(TabHistoryEntryItemViewModel.IsSelected))
                    {
                        ExportPdfCommand.NotifyCanExecuteChanged();
                    }
                };
                monthVm.AddEntry(entryVm);
            }

            MonthGroups.Add(monthVm);
        }

        ExportPdfCommand.NotifyCanExecuteChanged();
    }

    private void SelectAllEntries()
    {
        foreach (var g in MonthGroups)
        {
            g.IsSelected = true;
        }
    }

    private void SelectNoEntries()
    {
        foreach (var g in MonthGroups)
        {
            g.IsSelected = false;
        }
    }

    private bool CanExportSelected() => !IsExporting && GetSelectedRows().Count > 0;

    private List<TabHistoryEntryRow> GetSelectedRows()
    {
        var list = new List<TabHistoryEntryRow>();
        foreach (var g in MonthGroups)
        {
            foreach (var e in g.Entries)
            {
                if (e.IsSelected)
                {
                    list.Add(e.Source);
                }
            }
        }

        return list;
    }

    private async Task ExportSelectedPdfAsync()
    {
        var selected = GetSelectedRows()
            .Select(r => (Row: r, Line: TabHistoryEntryDisplayFormatter.ParseLedgerLine(r)))
            .OrderByDescending(x => x.Line.WhenLocal ?? DateTimeOffset.MinValue)
            .Select(x => x.Row)
            .ToList();
        if (selected.Count == 0 || string.IsNullOrWhiteSpace(_session.TabLegacyId))
        {
            Status = "Select at least one entry (or tick a month header) to export.";
            return;
        }

        IsExporting = true;
        Status = "Building PDF…";
        try
        {
            var tabName = string.IsNullOrWhiteSpace(_session.TabDisplayName)
                ? _session.TabLegacyId!.Trim()
                : _session.TabDisplayName.Trim();
            var rangeCaption = BuildExportRangeCaption(selected);
            var bytes = TabHistoryPdfBuilder.BuildV2Style(tabName, rangeCaption, selected);
            var safe = SanitizeFileSegment(tabName);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
            var fileName = $"TabHistory_{safe}_{stamp}.pdf";
            var dir = _paths.GetTabHistoryExportsDirectory();
            var path = Path.Combine(dir, fileName);
            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(true);

            if (_launcher.TryLaunch(path))
            {
                Status = $"Saved and opened: {path}";
            }
            else if (_launcher.RevealInExplorer(path))
            {
                Status = $"Saved to: {path}";
            }
            else
            {
                Status = $"Saved to: {path}";
            }
        }
        catch (Exception ex)
        {
            Status = $"PDF export failed: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
            ExportPdfCommand.NotifyCanExecuteChanged();
        }
    }

    private static readonly CultureInfo ExportCulture = CultureInfo.GetCultureInfo("en-AU");

    private string BuildExportRangeCaption(IReadOnlyList<TabHistoryEntryRow> selected)
    {
        var totalVisible = MonthGroups.Sum(g => g.Entries.Count);
        if (selected.Count == totalVisible && totalVisible > 0)
        {
            return RangeSummary;
        }

        var months = new SortedSet<DateTime>();
        DateTimeOffset? min = null;
        DateTimeOffset? max = null;
        foreach (var row in selected)
        {
            var line = TabHistoryEntryDisplayFormatter.ParseLedgerLine(row);
            if (line.WhenLocal is { } when)
            {
                months.Add(new DateTime(when.Year, when.Month, 1));
                if (min is null || when < min)
                {
                    min = when;
                }

                if (max is null || when > max)
                {
                    max = when;
                }
            }
        }

        if (months.Count == 0)
        {
            return selected.Count == 1 ? "1 selected entry" : $"{selected.Count} selected entries";
        }

        if (months.Count == 1)
        {
            return months.Min.ToString("MMMM yyyy", ExportCulture);
        }

        var firstMonth = months.Min;
        var lastMonth = months.Max;
        var spansContiguousMonths = months.Count == ((lastMonth.Year - firstMonth.Year) * 12 + lastMonth.Month - firstMonth.Month + 1);
        if (spansContiguousMonths)
        {
            return $"{firstMonth.ToString("MMMM yyyy", ExportCulture)} — {lastMonth.ToString("MMMM yyyy", ExportCulture)}";
        }

        if (months.Count <= 4)
        {
            return string.Join(", ", months.Select(m => m.ToString("MMMM yyyy", ExportCulture)));
        }

        if (min is { } lo && max is { } hi)
        {
            return $"{lo.LocalDateTime.ToString("d MMM yyyy", ExportCulture)} — {hi.LocalDateTime.ToString("d MMM yyyy", ExportCulture)}";
        }

        return $"{months.Count} months";
    }

    private bool TryGetCustomUtcBounds(out DateTimeOffset startInclusiveUtc, out DateTimeOffset endExclusiveUtc, out string label)
    {
        startInclusiveUtc = default;
        endExclusiveUtc = default;
        label = string.Empty;

        var startLocal = CustomStart.LocalDateTime.Date;
        var endLocal = CustomEnd.LocalDateTime.Date;
        if (endLocal < startLocal)
        {
            return false;
        }

        var start = new DateTimeOffset(startLocal, TimeZoneInfo.Local.GetUtcOffset(startLocal));
        var endExclusive = new DateTimeOffset(endLocal.AddDays(1), TimeZoneInfo.Local.GetUtcOffset(endLocal.AddDays(1)));
        startInclusiveUtc = start.ToUniversalTime();
        endExclusiveUtc = endExclusive.ToUniversalTime();
        label = $"{startLocal:d} — {endLocal:d}";
        return true;
    }

    private static bool TryGetPresetUtcBounds(
        TabHistoryRangePreset preset,
        out DateTimeOffset startInclusiveUtc,
        out DateTimeOffset endExclusiveUtc,
        out string caption)
    {
        var now = DateTimeOffset.Now;
        var todayLocal = now.LocalDateTime.Date;

        switch (preset)
        {
            case TabHistoryRangePreset.ThisMonth:
            {
                var first = new DateTime(todayLocal.Year, todayLocal.Month, 1, 0, 0, 0, DateTimeKind.Local);
                var next = first.AddMonths(1);
                startInclusiveUtc = LocalMidnightToUtc(first);
                endExclusiveUtc = LocalMidnightToUtc(next);
                caption = $"{first:MMMM yyyy} (this month)";
                return true;
            }

            case TabHistoryRangePreset.LastMonth:
            {
                var thisMonthFirst = new DateTime(todayLocal.Year, todayLocal.Month, 1, 0, 0, 0, DateTimeKind.Local);
                var lastMonthFirst = thisMonthFirst.AddMonths(-1);
                startInclusiveUtc = LocalMidnightToUtc(lastMonthFirst);
                endExclusiveUtc = LocalMidnightToUtc(thisMonthFirst);
                caption = $"{lastMonthFirst:MMMM yyyy}";
                return true;
            }

            case TabHistoryRangePreset.Last90Days:
            {
                var start = todayLocal.AddDays(-89);
                var startDto = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start));
                var tomorrow = todayLocal.AddDays(1);
                var endEx = new DateTimeOffset(tomorrow, TimeZoneInfo.Local.GetUtcOffset(tomorrow));
                startInclusiveUtc = startDto.ToUniversalTime();
                endExclusiveUtc = endEx.ToUniversalTime();
                caption = "Last 90 days";
                return true;
            }

            default:
                startInclusiveUtc = default;
                endExclusiveUtc = default;
                caption = string.Empty;
                return false;
        }
    }

    private static DateTimeOffset LocalMidnightToUtc(DateTime localDate)
    {
        var dto = new DateTimeOffset(localDate, TimeZoneInfo.Local.GetUtcOffset(localDate));
        return dto.ToUniversalTime();
    }

    private static string SanitizeFileSegment(string name)
    {
        var chars = Path.GetInvalidFileNameChars();
        var s = string.Join("_", name.Split(chars, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrEmpty(s))
        {
            return "tab";
        }

        return s.Length > 48 ? s[..48] : s;
    }

    private void Close()
    {
        _slide.Close();
        _session.Clear();
    }
}
