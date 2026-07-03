using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NickeltownPOSV4.ViewModels;

public sealed class StockFilterCardViewModel : ObservableObject
{
    private int _count;
    private bool _isSelected;

    public StockFilterCardViewModel(int filterIndex, string title, string glyph, string accentColor)
    {
        FilterIndex = filterIndex;
        Title = title;
        Glyph = glyph;
        AccentColor = accentColor;
    }

    public int FilterIndex { get; }
    public string Title { get; }
    public string Glyph { get; }
    public string AccentColor { get; }
    public string CompactLabel => Title + " (" + Count.ToString(CultureInfo.InvariantCulture) + ")";
    public string ChipBackground => IsSelected ? AccentColor : "#FFFDFEFF";
    public string ChipForeground => IsSelected
        ? "#FFFFFFFF"
        : (FilterIndex == Services.Stock.StockManagementBrowserFilter.ChipInactive ? "#FF6B7280" : AccentColor);
    public string ChipBorderBrush => IsSelected ? AccentColor : "#FFE2E8F0";

    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(CompactLabel));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        private set
        {
            if (SetProperty(ref _isSelected, value))
            {
                OnPropertyChanged(nameof(ChipBackground));
                OnPropertyChanged(nameof(ChipForeground));
                OnPropertyChanged(nameof(ChipBorderBrush));
            }
        }
    }

    public void SetSelected(bool selected) => IsSelected = selected;

    public static string AccentForFilter(int filterIndex) => filterIndex switch
    {
        Services.Stock.StockManagementBrowserFilter.ChipNeedBuying => "#FFF59E0B",
        Services.Stock.StockManagementBrowserFilter.ChipOutOfStock => "#FFDC2626",
        Services.Stock.StockManagementBrowserFilter.ChipDrinks => "#FF2563EB",
        Services.Stock.StockManagementBrowserFilter.ChipMerch => "#FF7C3AED",
        Services.Stock.StockManagementBrowserFilter.ChipInactive => "#FF6B7280",
        _ => "#FF2563EB",
    };
}