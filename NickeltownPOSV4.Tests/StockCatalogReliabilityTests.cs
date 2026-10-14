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
    public void SkipsSoldItemStockDecrement_TrackedFoodDoesNotSkip()
    {
        var skip = StockSaleDeductionHelper.SkipsSoldItemStockDecrement(
            trackStock: 1,
            orderInMerchandise: 0,
            stockMode: StockCatalogTaxonomy.StockModeTracked,
            itemType: "Item",
            itemName: "Steak Sandwich");

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
