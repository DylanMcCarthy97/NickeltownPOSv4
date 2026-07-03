using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.ViewModels;

public sealed class TabsBoardCellViewModel : ObservableViewModel
{
    private TabCardModel? _tab;
    private TabsBoardCellKind _kind = TabsBoardCellKind.Tab;

    public TabsBoardCellKind Kind => _kind;

    public TabCardModel? Tab => _tab;

    public bool IsTabCell => _kind == TabsBoardCellKind.Tab && _tab is not null;

    public bool IsAddCell => _kind is TabsBoardCellKind.NewMemberTab or TabsBoardCellKind.NewGuestTab;

    public bool IsEmpty => !IsTabCell && !IsAddCell;

    public bool IsNewMemberCell => _kind == TabsBoardCellKind.NewMemberTab;

    public bool IsNewGuestCell => _kind == TabsBoardCellKind.NewGuestTab;

    public string TabDisplayName => Tab?.DisplayName ?? string.Empty;

    public string TabMemberBadge => Tab?.MemberBadge ?? string.Empty;

    public string TabBalanceText => Tab?.BalanceText ?? string.Empty;

    public string TabStatusLabel => Tab?.BalanceStatusLabel ?? string.Empty;

    public TabBalanceTier DisplayBalanceTier => Tab?.BalanceTier ?? TabBalanceTier.Good;

    /// <summary>Guest tabs use the guest strip; members use balance tier (same rules as balance text).</summary>
    public object TabStripBrushSource =>
        Tab is { IsGuest: true } guestTab ? guestTab : DisplayBalanceTier;

    public string TabActivityLine => Tab?.LastDrinkLine ?? string.Empty;

    public string TabLastUpdatedText => Tab?.LastUpdatedText ?? string.Empty;

    public string TabFooterLine => Tab?.FooterLine ?? string.Empty;

    public bool TabIsSelected => Tab?.IsSelected ?? false;

    public bool TabIsGuest => Tab?.IsGuest ?? false;

    public string AddCardTitle => "New Tab";

    public string AddCardSubtitle =>
        IsNewGuestCell ? "Create guest tab" : "Create member / family tab";

    public void Clear()
    {
        if (_tab is not null)
        {
            _tab.PropertyChanged -= OnTabPropertyChanged;
            _tab = null;
        }

        _kind = TabsBoardCellKind.Tab;
        RaiseAll();
    }

    public void AttachTab(TabCardModel tab)
    {
        if (_tab is not null)
        {
            _tab.PropertyChanged -= OnTabPropertyChanged;
        }

        _tab = tab;
        _kind = TabsBoardCellKind.Tab;
        _tab.PropertyChanged += OnTabPropertyChanged;
        RaiseAll();
    }

    public void SetAddCard(TabsBoardCellKind kind)
    {
        if (_tab is not null)
        {
            _tab.PropertyChanged -= OnTabPropertyChanged;
            _tab = null;
        }

        _kind = kind;
        RaiseAll();
    }

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(TabIsSelected));
        OnPropertyChanged(string.Empty);
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        RaiseBridged();

    private void RaiseAll()
    {
        RaiseBridged();
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(IsTabCell));
        OnPropertyChanged(nameof(IsAddCell));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsNewMemberCell));
        OnPropertyChanged(nameof(IsNewGuestCell));
        OnPropertyChanged(nameof(AddCardTitle));
        OnPropertyChanged(nameof(AddCardSubtitle));
    }

    private void RaiseBridged()
    {
        OnPropertyChanged(nameof(Tab));
        OnPropertyChanged(nameof(TabDisplayName));
        OnPropertyChanged(nameof(TabMemberBadge));
        OnPropertyChanged(nameof(TabBalanceText));
        OnPropertyChanged(nameof(TabStatusLabel));
        OnPropertyChanged(nameof(DisplayBalanceTier));
        OnPropertyChanged(nameof(TabStripBrushSource));
        OnPropertyChanged(nameof(TabActivityLine));
        OnPropertyChanged(nameof(TabLastUpdatedText));
        OnPropertyChanged(nameof(TabFooterLine));
        OnPropertyChanged(nameof(TabIsSelected));
        OnPropertyChanged(nameof(TabIsGuest));
        OnPropertyChanged(string.Empty);
    }
}