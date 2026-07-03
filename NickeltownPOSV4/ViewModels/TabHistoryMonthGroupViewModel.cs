using System;
using System.Collections.ObjectModel;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.ViewModels;

/// <summary>One ledger row formatted like NickeltownBarPOS (V2) tab history.</summary>
public sealed class TabHistoryLedgerLine
{
    public DateTimeOffset? WhenLocal { get; init; }

    public string WhenDisplay { get; init; } = "-";

    public string MonthGroupLabel { get; init; } = "Unknown";

    public string Type { get; init; } = "-";

    public string PaymentMethod { get; init; } = "-";

    public string AmountDisplay { get; init; } = "-";

    public string Bartender { get; init; } = "-";

    public string Details { get; init; } = string.Empty;

    /// <summary>WinUI color for entry type (ARGB hex).</summary>
    public string TypeColorArgb { get; init; } = "#FF111827";

    public string SummaryText =>
        $"{WhenDisplay} | {Type} | {PaymentMethod} | {AmountDisplay} | {Bartender} | {Details}";
}

public sealed class TabHistoryEntryItemViewModel : ObservableViewModel
{
    private bool _isSelected;

    public TabHistoryEntryItemViewModel(TabHistoryEntryRow source, TabHistoryLedgerLine line)
    {
        Source = source;
        Line = line;
    }

    public TabHistoryEntryRow Source { get; }

    public TabHistoryLedgerLine Line { get; }

    public string SummaryText => Line.SummaryText;

    public string WhenDisplay => Line.WhenDisplay;

    public string EntryType => Line.Type;

    public string PaymentMethod => Line.PaymentMethod;

    public string AmountDisplay => Line.AmountDisplay;

    public string Bartender => Line.Bartender;

    public string Details => Line.Details;

    public string TypeColorArgb => Line.TypeColorArgb;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class TabHistoryMonthGroupViewModel : ObservableViewModel
{
    private bool _isSelected;

    private bool _suppressChildSync;

    public TabHistoryMonthGroupViewModel(string monthLabel)
    {
        MonthLabel = monthLabel;
        Entries = new ObservableCollection<TabHistoryEntryItemViewModel>();
    }

    public string MonthLabel { get; }

    public ObservableCollection<TabHistoryEntryItemViewModel> Entries { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            _suppressChildSync = true;
            foreach (var e in Entries)
            {
                e.IsSelected = value;
            }

            _suppressChildSync = false;
        }
    }

    public void AddEntry(TabHistoryEntryItemViewModel entry)
    {
        entry.PropertyChanged += (_, e) =>
        {
            if (_suppressChildSync || e.PropertyName != nameof(TabHistoryEntryItemViewModel.IsSelected))
            {
                return;
            }

            SyncMonthFromChildren();
        };
        Entries.Add(entry);
    }

    private void SyncMonthFromChildren()
    {
        if (Entries.Count == 0)
        {
            return;
        }

        var all = true;
        foreach (var e in Entries)
        {
            if (!e.IsSelected)
            {
                all = false;
                break;
            }
        }

        _isSelected = all;
        OnPropertyChanged(nameof(IsSelected));
    }
}
