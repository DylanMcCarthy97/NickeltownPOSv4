using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Stock;
using Xunit;

namespace ShoppingListLogic.Tests;

public sealed class ShoppingListSplitTests
{
    [Fact]
    public void Categories_SplitBarAndMerchandise_ExcludeFood()
    {
        var lager = Row(1, "Lager", "Beer", 0, 2, 12, 6);
        var cola = Row(2, "Cola", "Drinks", 1, 4, 24, 24);
        var hoodie = Row(3, "Club Hoodie", "Merch", 2, 3, 8, 1);
        var pie = Row(4, "Meat Pie", "Food", 0, 2, 10, 1);

        Assert.True(ShoppingListCatalog.IsBarShoppingCategory(lager));
        Assert.True(ShoppingListCatalog.IsBarShoppingCategory(cola));
        Assert.False(ShoppingListCatalog.IsBarShoppingCategory(hoodie));
        Assert.False(ShoppingListCatalog.IsBarShoppingCategory(pie));
        Assert.True(ShoppingListCatalog.IsMerchandiseShoppingCategory(hoodie));
        Assert.False(ShoppingListCatalog.IsMerchandiseShoppingCategory(lager));
    }

    [Fact]
    public void Snapshot_BarNeverIncludesMerch_AndNeverZeroBuys()
    {
        var rows = new List<StockEditorRow>
        {
            Row(1, "Lager", "Beer", 0, 2, 12, 6),
            Row(2, "Plenty", "Beer", 40, 2, 12, 6),
            Row(3, "Club Cap", "Merch", 1, 3, 10, 1),
            Row(4, "Banner", "Merch", 0, 1, 2, 1),
            Row(5, "Broken", "Spirits", 0, 0, 0, 6),
            Row(6, "Soft Drink", "Drinks", 3, 6, 24, 0),
        };

        var bar = ShoppingListSnapshot.Build(rows, ShoppingListKind.Bar);
        Assert.Equal(2, bar.PurchaseRows.Count);
        Assert.All(bar.PurchaseRows, r => Assert.True(r.UnitsToBuy > 0));
        Assert.DoesNotContain(bar.PurchaseRows, r => r.Name is "Club Cap" or "Banner" or "Plenty");
        Assert.Contains("Broken", bar.InvalidReorderNames);
        Assert.Contains("Soft Drink", bar.MissingPackSizeNames);

        var lager = bar.PurchaseRows.Single(r => r.Name == "Lager");
        Assert.Equal(12, lager.UnitsToBuy);
        Assert.Equal(2, lager.PacksToBuy);

        var merch = ShoppingListSnapshot.Build(rows, ShoppingListKind.Merchandise);
        Assert.Equal(2, merch.PurchaseRows.Count);
        Assert.Contains(merch.PurchaseRows, r => r.Name == "Club Cap");
        Assert.Contains(merch.PurchaseRows, r => r.Name == "Banner");
        Assert.DoesNotContain(merch.PurchaseRows, r => r.Name == "Lager");
    }

    [Fact]
    public void PackSize_CeilDiv_AndMissingPackLeavesUnits()
    {
        var withPack = Row(1, "Beer Case", "Beer", 2, 4, 14, 6);
        Assert.Equal(12, StockInventoryLevelHelper.NeedQty(withPack));
        Assert.Equal(2, StockInventoryLevelHelper.ResolvePacksToBuy(withPack));

        var noPack = Row(2, "Mystery", "Drinks", 1, 2, 10, 0);
        Assert.Equal(9, StockInventoryLevelHelper.NeedQty(noPack));
        Assert.Null(StockInventoryLevelHelper.ResolvePacksToBuy(noPack));
    }

    private static StockEditorRow Row(
        long id, string name, string sub, int stock, int warn, int preferred, int pack) =>
        new()
        {
            ItemId = id,
            Name = name,
            IsActive = 1,
            TrackStock = 1,
            StockQty = stock,
            WarnMeBelow = warn > 0 ? warn : null,
            PreferredStockLevel = preferred > 0 ? preferred : null,
            PurchaseUnitQty = pack > 0 ? pack : null,
            CatalogSubCategory = sub,
            StockMode = StockCatalogTaxonomy.StockModeTracked,
            ShowOnShoppingList = 1,
        };
}
