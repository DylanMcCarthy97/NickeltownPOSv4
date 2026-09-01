using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.ViewModels;

public sealed class QuickFreeItemButtonViewModel : ObservableViewModel
{
    public QuickFreeItemButtonViewModel(QuickFreeButtonRow row, IAsyncRelayCommand<QuickFreeItemButtonViewModel> recordCommand)
    {
        ItemId = row.ItemId;
        ProductName = row.ProductName;
        DisplayLabel = string.IsNullOrWhiteSpace(row.DisplayLabel) ? row.ProductName : row.DisplayLabel.Trim();
        Icon = string.IsNullOrWhiteSpace(row.Icon) ? string.Empty : row.Icon.Trim();
        TodayCount = row.TodayCount;
        StockQty = row.StockQty;
        TrackStock = row.TrackStock != 0;
        IsOutOfStock = TrackStock && StockQty <= 0;
        RecordCommand = recordCommand;
    }

    public long ItemId { get; }

    public string ProductName { get; }

    public string DisplayLabel { get; }

    public string Icon { get; }

    public bool HasIcon => Icon.Length > 0;

    public string TitleText => (string.IsNullOrWhiteSpace(DisplayLabel) ? ProductName : DisplayLabel).ToUpperInvariant();

    public int TodayCount { get; }

    public string TodayText => $"{TodayCount} today";

    public int StockQty { get; }

    public bool TrackStock { get; }

    public bool IsOutOfStock { get; }

    public string StockText =>
        !TrackStock ? string.Empty
        : IsOutOfStock ? "Stock: 0"
        : $"Stock: {StockQty}";

    public bool CanRecord => !IsOutOfStock;

    public IAsyncRelayCommand<QuickFreeItemButtonViewModel> RecordCommand { get; }
}
