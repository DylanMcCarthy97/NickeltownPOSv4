using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>One shopping-list kind built from a stock snapshot — purchase rows vs setup review.</summary>
public sealed class ShoppingListSnapshot
{
    public required ShoppingListKind Kind { get; init; }

    /// <summary>Products with a positive units-to-buy quantity (genuine purchase requirements).</summary>
    public required IReadOnlyList<StockShoppingListRowViewModel> PurchaseRows { get; init; }

    /// <summary>Needs buying status but no valid target/warn — cannot compute a purchase qty.</summary>
    public required IReadOnlyList<string> InvalidReorderNames { get; init; }

    /// <summary>Purchase rows missing pack size (shown once at PDF footer, not per row).</summary>
    public required IReadOnlyList<string> MissingPackSizeNames { get; init; }

    public int TotalUnitsToBuy => PurchaseRows.Sum(r => r.UnitsToBuy);

    public static ShoppingListSnapshot Build(IEnumerable<StockEditorRow> stockRows, ShoppingListKind kind)
    {
        var inKind = stockRows.Where(r => ShoppingListCatalog.BelongsTo(kind, r)).ToList();

        var purchase = new List<StockShoppingListRowViewModel>();
        var invalid = new List<string>();
        var missingPack = new List<string>();

        foreach (var row in inKind)
        {
            if (!StockInventoryLevelHelper.ShowOnShoppingList(row))
            {
                continue;
            }

            var status = StockInventoryLevelHelper.ResolveStatus(row);
            var needsAttention = status is StockVolunteerStatus.OutOfStock
                or StockVolunteerStatus.BuyNow
                or StockVolunteerStatus.BuySoon;
            if (!needsAttention)
            {
                continue;
            }

            var units = StockInventoryLevelHelper.NeedQty(row);
            if (units <= 0)
            {
                // Out/low stock but no Preferred/Warn target — review settings, not a purchase line.
                invalid.Add(row.Name);
                continue;
            }

            var vm = new StockShoppingListRowViewModel(row);
            purchase.Add(vm);
            if (vm.PacksToBuy is null)
            {
                missingPack.Add(row.Name);
            }
        }

        purchase = purchase
            .OrderBy(r => r.SortPriority)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ShoppingListSnapshot
        {
            Kind = kind,
            PurchaseRows = purchase,
            InvalidReorderNames = invalid
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            MissingPackSizeNames = missingPack
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }
}
