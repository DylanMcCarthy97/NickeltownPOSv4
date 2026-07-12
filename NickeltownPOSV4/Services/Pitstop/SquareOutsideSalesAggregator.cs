using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Services.Pitstop;

public static class SquareOutsideSalesAggregator
{
    public static IReadOnlyList<CombinedOutsideSaleRow> BuildCombinedOutsideSales(
        IReadOnlyList<OutsideItemSaleRow> manualCashLines,
        IReadOnlyList<PitstopProductAggregateRow> squareCardProducts)
    {
        var rows = new List<CombinedOutsideSaleRow>();
        var remainingSquare = squareCardProducts.ToList();

        foreach (var cash in manualCashLines)
        {
            var square = FindSquareMatch(cash, remainingSquare);
            if (square is not null)
            {
                remainingSquare.Remove(square);
            }

            if (cash.CashQty <= 0 && cash.CashDollars <= 0m && square is null
                && cash.CardQty <= 0 && cash.CardDollars <= 0m)
            {
                continue;
            }

            rows.Add(new CombinedOutsideSaleRow
            {
                PitstopItemId = cash.PitstopItemId,
                Name = cash.DisplayLabel,
                CategoryName = cash.OutsideLineKind == PitstopOutsideLineCatalogBuilder.LineKindRaffle
                    ? EventReportCategoryNormalizer.Other
                    : EventReportCategoryNormalizer.Merchandise,
                CashQuantity = cash.CashQty,
                CashTotal = Round(cash.CashDollars),
                CardQuantity = square?.Quantity ?? cash.CardQty,
                CardTotal = square?.LineTotal ?? (cash.CardDollars > 0m ? Round(cash.CardDollars) : 0m),
            });
        }

        rows.AddRange(remainingSquare.Select(square => new CombinedOutsideSaleRow
        {
            PitstopItemId = square.ItemId > 0 ? square.ItemId : null,
            Name = square.Name,
            CategoryName = square.CategoryName,
            CardQuantity = square.Quantity,
            CardTotal = square.LineTotal,
        }));

        return rows
            .OrderBy(r => r.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<PitstopProductAggregateRow> AggregateProducts(IEnumerable<SquareOrderLineItemRow> lines)
    {
        return lines
            .GroupBy(l => (
                ItemId: l.MappedClubPosItemId ?? 0,
                Name: string.IsNullOrWhiteSpace(l.MappedClubPosItemName) ? l.ItemName : l.MappedClubPosItemName!,
                Category: l.CategoryName))
            .Select(g => new PitstopProductAggregateRow
            {
                ItemId = g.Key.ItemId,
                Name = g.Key.Name,
                CategoryName = g.Key.Category,
                Quantity = g.Sum(x => x.Quantity),
                LineTotal = Round(g.Sum(x => x.LineTotal)),
            })
            .OrderByDescending(p => p.LineTotal)
            .ToList();
    }

    public static IReadOnlyList<PitstopCategoryAggregateRow> AggregateCategories(IEnumerable<SquareOrderLineItemRow> lines)
    {
        return lines
            .GroupBy(l => l.CategoryName)
            .Select(g => new PitstopCategoryAggregateRow
            {
                CategoryName = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                LineTotal = Round(g.Sum(x => x.LineTotal)),
            })
            .OrderByDescending(c => c.LineTotal)
            .ToList();
    }

    public static IReadOnlyList<PitstopProductAggregateRow> MergeProductSales(
        IReadOnlyList<PitstopProductAggregateRow> clubPos,
        IReadOnlyList<PitstopProductAggregateRow> outside)
    {
        return clubPos
            .Concat(outside)
            .GroupBy(p => (p.ItemId > 0 ? p.ItemId : 0, NormalizeName(p.Name), p.CategoryName))
            .Select(g =>
            {
                var first = g.First();
                return new PitstopProductAggregateRow
                {
                    ItemId = g.Key.Item1 > 0 ? g.Key.Item1 : first.ItemId,
                    Name = first.Name,
                    CategoryName = first.CategoryName,
                    Quantity = g.Sum(x => x.Quantity),
                    LineTotal = Round(g.Sum(x => x.LineTotal)),
                };
            })
            .OrderByDescending(p => p.LineTotal)
            .ToList();
    }

    public static IReadOnlyList<PitstopCategoryAggregateRow> MergeCategorySales(
        IReadOnlyList<PitstopCategoryAggregateRow> clubPos,
        IReadOnlyList<PitstopCategoryAggregateRow> outside)
    {
        var normalizedClubPos = clubPos
            .GroupBy(c => EventReportCategoryNormalizer.Normalize(c.CategoryName, null))
            .Select(g => new PitstopCategoryAggregateRow
            {
                CategoryName = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                LineTotal = Round(g.Sum(x => x.LineTotal)),
            })
            .ToList();

        var normalizedOutside = outside
            .GroupBy(c => c.CategoryName)
            .Select(g => new PitstopCategoryAggregateRow
            {
                CategoryName = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                LineTotal = Round(g.Sum(x => x.LineTotal)),
            })
            .ToList();

        return normalizedClubPos
            .Concat(normalizedOutside)
            .GroupBy(c => c.CategoryName)
            .Select(g => new PitstopCategoryAggregateRow
            {
                CategoryName = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                LineTotal = Round(g.Sum(x => x.LineTotal)),
            })
            .OrderByDescending(c => c.LineTotal)
            .ToList();
    }

    public static IReadOnlyList<EventCategoryComparisonRow> BuildCategoryComparison(
        IReadOnlyList<PitstopCategoryAggregateRow> clubPos,
        IReadOnlyList<PitstopCategoryAggregateRow> outside,
        IReadOnlyList<PitstopCategoryAggregateRow> combined)
    {
        var clubPosMap = clubPos
            .GroupBy(c => EventReportCategoryNormalizer.Normalize(c.CategoryName, null))
            .ToDictionary(
                g => g.Key,
                g => new PitstopCategoryAggregateRow
                {
                    CategoryName = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    LineTotal = Round(g.Sum(x => x.LineTotal)),
                },
                StringComparer.OrdinalIgnoreCase);

        var outsideMap = outside
            .GroupBy(c => c.CategoryName)
            .ToDictionary(
                g => g.Key,
                g => new PitstopCategoryAggregateRow
                {
                    CategoryName = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    LineTotal = Round(g.Sum(x => x.LineTotal)),
                },
                StringComparer.OrdinalIgnoreCase);

        var combinedMap = combined
            .GroupBy(c => c.CategoryName)
            .ToDictionary(
                g => g.Key,
                g => new PitstopCategoryAggregateRow
                {
                    CategoryName = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    LineTotal = Round(g.Sum(x => x.LineTotal)),
                },
                StringComparer.OrdinalIgnoreCase);

        var categories = clubPosMap.Keys
            .Concat(outsideMap.Keys)
            .Concat(combinedMap.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => CategorySortKey(c))
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return categories
            .Select(category =>
            {
                clubPosMap.TryGetValue(category, out var pos);
                outsideMap.TryGetValue(category, out var outSide);
                combinedMap.TryGetValue(category, out var total);
                return new EventCategoryComparisonRow
                {
                    CategoryName = category,
                    ClubPosQuantity = pos?.Quantity ?? 0,
                    ClubPosLineTotal = pos?.LineTotal ?? 0m,
                    OutsideTerminalQuantity = outSide?.Quantity ?? 0,
                    OutsideTerminalLineTotal = outSide?.LineTotal ?? 0m,
                    CombinedQuantity = total?.Quantity ?? 0,
                    CombinedLineTotal = total?.LineTotal ?? 0m,
                };
            })
            .ToList();
    }

    private static int CategorySortKey(string category) => category switch
    {
        EventReportCategoryNormalizer.Food => 0,
        EventReportCategoryNormalizer.Drinks => 1,
        EventReportCategoryNormalizer.Merchandise => 2,
        EventReportCategoryNormalizer.Memberships => 3,
        _ => 99,
    };

    private static PitstopProductAggregateRow? FindSquareMatch(
        OutsideItemSaleRow cash,
        IReadOnlyList<PitstopProductAggregateRow> squareProducts)
    {
        if (cash.PitstopItemId is long itemId && itemId > 0)
        {
            var byId = squareProducts.FirstOrDefault(p => p.ItemId == itemId);
            if (byId is not null)
            {
                return byId;
            }
        }

        return squareProducts.FirstOrDefault(p =>
            string.Equals(NormalizeName(p.Name), NormalizeName(cash.DisplayLabel), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeName(string name) => name.Trim();

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
