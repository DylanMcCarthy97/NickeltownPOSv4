using System;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Mirrors production StockShoppingListRowViewModel for Linux logic tests.</summary>
public sealed class StockShoppingListRowViewModel
{
    public StockShoppingListRowViewModel(StockEditorRow row)
    {
        ItemId = row.ItemId;
        Name = row.Name;
        HaveQty = row.StockQty;
        UnitsToBuy = StockInventoryLevelHelper.NeedQty(row);
        NeedQty = UnitsToBuy;
        Status = StockInventoryLevelHelper.ResolveStatus(row);
        PacksToBuy = StockInventoryLevelHelper.ResolvePacksToBuy(row);
        HasSetupWarning = PacksToBuy is null && UnitsToBuy > 0;
        IsMerch = StockInventoryLevelHelper.IsMerchItem(row);
        SortPriority = Status switch
        {
            StockVolunteerStatus.OutOfStock => 0,
            StockVolunteerStatus.BuyNow => 1,
            StockVolunteerStatus.BuySoon => 2,
            _ => 3,
        };
    }

    public long ItemId { get; }
    public string Name { get; }
    public int HaveQty { get; }
    public int UnitsToBuy { get; }
    public int NeedQty { get; }
    public int? PacksToBuy { get; }
    public StockVolunteerStatus Status { get; }
    public int SortPriority { get; }
    public bool HasSetupWarning { get; }
    public bool IsMerch { get; }
}
