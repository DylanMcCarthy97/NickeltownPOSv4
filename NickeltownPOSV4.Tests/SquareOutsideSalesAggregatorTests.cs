using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class SquareOutsideSalesAggregatorTests
{
    [Fact]
    public void AggregateProducts_groups_by_mapped_product()
    {
        var lines = new List<SquareOrderLineItemRow>
        {
            new() { ItemName = "Burger", CategoryName = EventReportCategoryNormalizer.Food, Quantity = 2, LineTotal = 36m, MappedClubPosItemId = 10, MappedClubPosItemName = "Burger" },
            new() { ItemName = "Burger", CategoryName = EventReportCategoryNormalizer.Food, Quantity = 1, LineTotal = 18m, MappedClubPosItemId = 10, MappedClubPosItemName = "Burger" },
            new() { ItemName = "Water", CategoryName = EventReportCategoryNormalizer.Drinks, Quantity = 3, LineTotal = 12m },
        };

        var products = SquareOutsideSalesAggregator.AggregateProducts(lines);

        Assert.Equal(2, products.Count);
        Assert.Equal(3, products[0].Quantity);
        Assert.Equal(54m, products[0].LineTotal);
    }

    [Fact]
    public void BuildCategoryComparison_merges_clubpos_and_outside_totals()
    {
        var clubPos = new List<PitstopCategoryAggregateRow>
        {
            new() { CategoryName = EventReportCategoryNormalizer.Food, Quantity = 10, LineTotal = 100m },
        };
        var outside = new List<PitstopCategoryAggregateRow>
        {
            new() { CategoryName = EventReportCategoryNormalizer.Food, Quantity = 5, LineTotal = 50m },
            new() { CategoryName = EventReportCategoryNormalizer.Drinks, Quantity = 8, LineTotal = 40m },
        };
        var combined = SquareOutsideSalesAggregator.MergeCategorySales(clubPos, outside);

        var comparison = SquareOutsideSalesAggregator.BuildCategoryComparison(clubPos, outside, combined);

        var food = Assert.Single(comparison, row => row.CategoryName == EventReportCategoryNormalizer.Food);
        Assert.Equal(10, food.ClubPosQuantity);
        Assert.Equal(5, food.OutsideTerminalQuantity);
        Assert.Equal(15, food.CombinedQuantity);
        Assert.Equal(150m, food.CombinedLineTotal);
    }

    [Fact]
    public void BuildCombinedOutsideSales_merges_manual_cash_with_square_card_by_item_id()
    {
        var cash = new List<OutsideItemSaleRow>
        {
            new()
            {
                DisplayLabel = "Club shirt",
                OutsideLineKind = PitstopOutsideLineCatalogBuilder.LineKindMerchSku,
                PitstopItemId = 42,
                CashQty = 3,
                CashDollars = 105m,
                CardQty = 99,
                CardDollars = 999m,
            },
        };
        var square = new List<PitstopProductAggregateRow>
        {
            new()
            {
                ItemId = 42,
                Name = "Club shirt",
                CategoryName = EventReportCategoryNormalizer.Merchandise,
                Quantity = 2,
                LineTotal = 70m,
            },
        };

        var row = Assert.Single(SquareOutsideSalesAggregator.BuildCombinedOutsideSales(cash, square));

        Assert.Equal(3, row.CashQuantity);
        Assert.Equal(105m, row.CashTotal);
        Assert.Equal(2, row.CardQuantity);
        Assert.Equal(70m, row.CardTotal);
        Assert.Equal(5, row.CombinedQuantity);
        Assert.Equal(175m, row.CombinedTotal);
    }
}
