namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Minimal StockEditorRow shape for shopping-list logic tests on Linux.</summary>
public sealed class StockEditorRow
{
    public long ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int IsActive { get; set; } = 1;
    public int TrackStock { get; set; } = 1;
    public int StockQty { get; set; }
    public int? WarnMeBelow { get; set; }
    public int? LowStockThreshold { get; set; }
    public int? PreferredStockLevel { get; set; }
    public int? PurchaseUnitQty { get; set; }
    public string? CatalogBucket { get; set; }
    public string? CatalogSubCategory { get; set; }
    public string? StockMode { get; set; }
    public int? ShowOnShoppingList { get; set; } = 1;
    public int OrderInMerchandise { get; set; }
    public int NotGonnaOrderBack { get; set; }
    public int IsRunOutItem { get; set; }
}
