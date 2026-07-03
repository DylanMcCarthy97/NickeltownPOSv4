using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using NickeltownPOSV4.Models;
using Windows.UI;

namespace NickeltownPOSV4.ViewModels;

/// <summary>One cell in the fixed 3×3 tabs board (may be empty).</summary>
public sealed class TabSlotViewModel : ObservableViewModel
{
    private TabCardModel? _tab;

    public TabCardModel? Tab
    {
        get => _tab;
        private set
        {
            if (_tab is not null)
            {
                _tab.PropertyChanged -= OnTabPropertyChanged;
            }

            if (SetProperty(ref _tab, value))
            {
                OnPropertyChanged(nameof(HasTab));
                RaiseBridged();
            }

            if (_tab is not null)
            {
                _tab.PropertyChanged += OnTabPropertyChanged;
            }
        }
    }

    public bool HasTab => Tab is not null;

    public string TabDisplayName => Tab?.DisplayName ?? string.Empty;

    public string TabMemberBadge => Tab?.MemberBadge ?? string.Empty;

    /// <summary>Guest tabs use a warmer badge tint so they stand out on the board.</summary>
    public Brush TabBadgeBackground =>
        Tab?.IsGuest == true
            ? new SolidColorBrush(Color.FromArgb(255, 255, 243, 224))
            : new SolidColorBrush(Color.FromArgb(255, 243, 246, 251));

    public Brush TabBadgeBorderBrush =>
        Tab?.IsGuest == true
            ? new SolidColorBrush(Color.FromArgb(255, 245, 158, 11))
            : new SolidColorBrush(Color.FromArgb(255, 211, 220, 232));

    public string TabBalanceText => Tab?.BalanceText ?? string.Empty;

    public TabBalanceTier DisplayBalanceTier => Tab?.BalanceTier ?? TabBalanceTier.Good;

    public string TabLastDrinkLine => Tab?.LastDrinkLine ?? string.Empty;

    public bool TabIsSelected => Tab?.IsSelected ?? false;

    public void SetTab(TabCardModel? tab) => Tab = tab;

    private void OnTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TabCardModel.IsSelected) or nameof(TabCardModel.DisplayName)
            or nameof(TabCardModel.BalanceText) or nameof(TabCardModel.BalanceTier)
            or nameof(TabCardModel.MemberBadge) or nameof(TabCardModel.LastDrinkLine) or nameof(TabCardModel.IsGuest))
        {
            RaiseBridged();
        }
    }

    private void RaiseBridged()
    {
        OnPropertyChanged(nameof(TabDisplayName));
        OnPropertyChanged(nameof(TabMemberBadge));
        OnPropertyChanged(nameof(TabBadgeBackground));
        OnPropertyChanged(nameof(TabBadgeBorderBrush));
        OnPropertyChanged(nameof(TabBalanceText));
        OnPropertyChanged(nameof(DisplayBalanceTier));
        OnPropertyChanged(nameof(TabLastDrinkLine));
        OnPropertyChanged(nameof(TabIsSelected));
    }
}
