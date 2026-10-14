using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Stock;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class StockCatalogReliabilityTests
{
    [Theory]
    [InlineData("9300675021234", "9300675021234", null, true)]
    [InlineData("00601234567890", "601234567890", null, true)]
    [InlineData("ALT-99", "PRIMARY", "[\"alt-99\",\"other\"]", true)]
    [InlineData("missing", "PRIMARY", "[\"alt-99\"]", false)]
    public void MatchesPrimaryOrAlternate_SupportsLeadingZerosAndAlternates(
        string scan,
        string? primary,
        string? alternatesJson,
        bool expected)
    {
        Assert.Equal(expected, StockAlternateSkuHelper.MatchesPrimaryOrAlternate(primary, alternatesJson, scan));
    }

    [Fact]
    public void SkipsSoldItemStockDecrement_FoodSubCategoryAlwaysSkips()
    {
        // Hot food has no counted amounts — even a drifted Tracked mode must not deduct.
        var skip = StockSaleDeductionHelper.SkipsSoldItemStockDecrement(
            trackStock: 1,
            orderInMerchandise: 0,
            stockMode: StockCatalogTaxonomy.StockModeTracked,
            itemType: "Item",
            itemName: "Steak Sandwich",
            catalogBucket: StockCatalogTaxonomy.BucketBar,
            catalogSubCategory: "Food");

        Assert.True(skip);
    }

    [Fact]
    public void SkipsSoldItemStockDecrement_TrackedDrinkStillDeducts()
    {
        var skip = StockSaleDeductionHelper.SkipsSoldItemStockDecrement(
            trackStock: 1,
            orderInMerchandise: 0,
            stockMode: StockCatalogTaxonomy.StockModeTracked,
            itemType: "Item",
            itemName: "Lager",
            catalogBucket: StockCatalogTaxonomy.BucketBar,
            catalogSubCategory: "Beer");

        Assert.False(skip);
    }

    [Fact]
    public void SkipsSoldItemStockDecrement_OrderInAndNotTrackedSkip()
    {
        Assert.True(StockSaleDeductionHelper.SkipsSoldItemStockDecrement(
            0, 1, StockCatalogTaxonomy.StockModeOrderIn, "Item", "Hat"));
        Assert.True(StockSaleDeductionHelper.SkipsSoldItemStockDecrement(
            0, 0, StockCatalogTaxonomy.StockModeNotTracked, "Item", "Sundry"));
    }

    [Fact]
    public void CatalogHealth_SharedBarOnlyPriceIsOk_AndMerchFoodNeedNoBarcode()
    {
        var rows = new List<StockEditorRow>
        {
            new()
            {
                ItemId = 1,
                Name = "Shared Drink",
                IsActive = 1,
                ShowInBar = 1,
                ShowInPitstop = 1,
                CatalogBucket = StockCatalogTaxonomy.BucketShared,
                CatalogSubCategory = "Drinks",
                StockMode = StockCatalogTaxonomy.StockModeTracked,
                TrackStock = 1,
                BarPrice = 5,
                PitstopPrice = null,
                WarnMeBelow = 2,
                Sku = "SHARED-1",
            },
            new()
            {
                ItemId = 2,
                Name = "Club Cap",
                IsActive = 1,
                ShowInBar = 1,
                CatalogBucket = StockCatalogTaxonomy.BucketBar,
                CatalogSubCategory = "Merch",
                StockMode = StockCatalogTaxonomy.StockModeTracked,
                TrackStock = 1,
                BarPrice = 20,
                WarnMeBelow = 1,
                Sku = null,
            },
            new()
            {
                ItemId = 4,
                Name = "Hot Chips Free Bar",
                IsActive = 1,
                ShowInBar = 1,
                ShowInPitstop = 1,
                CatalogBucket = StockCatalogTaxonomy.BucketPitstop,
                CatalogSubCategory = "Food",
                StockMode = StockCatalogTaxonomy.StockModeNotTracked,
                TrackStock = 0,
                BarPrice = 0,
                PitstopPrice = 8,
                Sku = null,
            },
            new()
            {
                ItemId = 5,
                Name = "Free pour + Pitstop bottle",
                IsActive = 1,
                ShowInBar = 1,
                ShowInPitstop = 1,
                CatalogBucket = StockCatalogTaxonomy.BucketShared,
                CatalogSubCategory = "Drinks",
                StockMode = StockCatalogTaxonomy.StockModeTracked,
                TrackStock = 1,
                BarPrice = null,
                PitstopPrice = 12,
                WarnMeBelow = 2,
                Sku = "SHARED-FREE-BAR",
            },
        };

        var issues = StockCatalogHealthService.Evaluate(rows);
        Assert.DoesNotContain(issues, i => i.ItemId == 1 && i.Kind == StockCatalogHealthService.IssueMissingChannelPrice);
        Assert.DoesNotContain(issues, i => i.ItemId == 2 && i.Kind == StockCatalogHealthService.IssueMissingBarcode);
        Assert.DoesNotContain(issues, i => i.ItemId == 4 && i.Kind == StockCatalogHealthService.IssueMissingBarcode);
        Assert.DoesNotContain(issues, i => i.ItemId == 4 && i.Kind == StockCatalogHealthService.IssueMissingChannelPrice);
        Assert.DoesNotContain(issues, i => i.ItemId == 5 && i.Kind == StockCatalogHealthService.IssueMissingChannelPrice);
    }

    [Fact]
    public void CatalogHealth_FlagsTrackedFood()
    {
        var rows = new List<StockEditorRow>
        {
            new()
            {
                ItemId = 3,
                Name = "Hot Chips",
                IsActive = 1,
                ShowInBar = 1,
                CatalogBucket = StockCatalogTaxonomy.BucketBar,
                CatalogSubCategory = "Food",
                StockMode = StockCatalogTaxonomy.StockModeTracked,
                TrackStock = 1,
                BarPrice = 8,
                Sku = "FOOD-1",
            },
        };

        var issues = StockCatalogHealthService.Evaluate(rows);
        Assert.Contains(issues, i => i.ItemId == 3 && i.Kind == StockCatalogHealthService.IssueFoodTracked);
    }

    [Fact]
    public void SkipsSoldItemStockDecrement_PrefersStockModeOverDriftedFlags()
    {
        // Mode Tracked but legacy TrackStock=0 — still deduct so counts stay accurate after mode sync.
        var skip = StockSaleDeductionHelper.SkipsSoldItemStockDecrement(
            trackStock: 0,
            orderInMerchandise: 0,
            stockMode: StockCatalogTaxonomy.StockModeTracked,
            itemType: "Item",
            itemName: "Lager");

        Assert.False(skip);
    }

    [Fact]
    public void ResolveTargetAmount_UsesPreferredStockLevelNotJsonPar()
    {
        var row = new StockEditorRow
        {
            PreferredStockLevel = 24,
            ItemDescription = """{"notes":"x","parLevel":99}""",
            LowStockThreshold = 2,
            WarnMeBelow = 3,
        };

        Assert.Equal(24, StockInventoryLevelHelper.ResolveTargetAmount(row));
    }

    [Fact]
    public void ResolveTargetAmount_FallsBackToWarnTimesTwoWhenNoPreferred()
    {
        var row = new StockEditorRow
        {
            PreferredStockLevel = null,
            WarnMeBelow = 5,
            ItemDescription = """{"parLevel":40}""",
        };

        Assert.Equal(10, StockInventoryLevelHelper.ResolveTargetAmount(row));
    }

    [Fact]
    public void ToStorageJson_DoesNotWriteParLevel()
    {
        var json = StockItemMetadataSerializer.ToStorageJson(
            new StockItemMetadataSerializer.StockItemMetadata
            {
                Notes = "hello",
                ParLevel = 12,
            },
            includeSpirits: false);

        Assert.Contains("hello", json, StringComparison.Ordinal);
        Assert.DoesNotContain("parLevel", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogHealth_FlagsMissingBarPriceAndModeDrift()
    {
        var rows = new List<StockEditorRow>
        {
            new()
            {
                ItemId = 1,
                Name = "No Price Lager",
                IsActive = 1,
                ShowInBar = 1,
                CatalogBucket = "Bar",
                CatalogSubCategory = "Beer",
                StockMode = StockCatalogTaxonomy.StockModeTracked,
                TrackStock = 1,
                BarPrice = null,
                UsesOpenPrice = 0,
            },
            new()
            {
                ItemId = 2,
                Name = "Drift Item",
                IsActive = 1,
                ShowInBar = 1,
                CatalogBucket = "Bar",
                CatalogSubCategory = "Drinks",
                StockMode = StockCatalogTaxonomy.StockModeTracked,
                TrackStock = 0,
                OrderInMerchandise = 1,
                BarPrice = 4,
                WarnMeBelow = 2,
                Sku = "SKU-2",
            },
        };

        var issues = StockCatalogHealthService.Evaluate(rows);
        Assert.Contains(issues, i => i.ItemId == 1 && i.Kind == StockCatalogHealthService.IssueMissingChannelPrice);
        Assert.Contains(issues, i => i.ItemId == 2 && i.Kind == StockCatalogHealthService.IssueModeFlagDrift);
    }

    [Fact]
    public void CatalogHealth_DetectsDuplicateBarcodes()
    {
        var rows = new List<StockEditorRow>
        {
            new()
            {
                ItemId = 10,
                Name = "A",
                IsActive = 1,
                Sku = "DUP",
                StockMode = StockCatalogTaxonomy.StockModeNotTracked,
                TrackStock = 0,
            },
            new()
            {
                ItemId = 11,
                Name = "B",
                IsActive = 1,
                AlternateSkusJson = """["dup"]""",
                StockMode = StockCatalogTaxonomy.StockModeNotTracked,
                TrackStock = 0,
            },
        };

        var issues = StockCatalogHealthService.Evaluate(rows);
        Assert.Contains(issues, i => i.Kind == StockCatalogHealthService.IssueDuplicateBarcode);
    }

    [Fact]
    public void MaintainsOnHandQuantity_FromStockMode()
    {
        Assert.True(StockCatalogTaxonomy.MaintainsOnHandQuantity(StockCatalogTaxonomy.StockModeTracked));
        Assert.True(StockCatalogTaxonomy.MaintainsOnHandQuantity(StockCatalogTaxonomy.StockModeSellUntilGone));
        Assert.False(StockCatalogTaxonomy.MaintainsOnHandQuantity(StockCatalogTaxonomy.StockModeOrderIn));
        Assert.False(StockCatalogTaxonomy.MaintainsOnHandQuantity(StockCatalogTaxonomy.StockModeNotTracked));
    }
}
