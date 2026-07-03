using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class StockCategoryRow
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Lower values appear first in POS category pickers.</summary>
    public int SortOrder { get; set; }

    public static StockCategoryRow Uncategorized() => new() { Id = -1, Name = "Uncategorized" };
}

public sealed class StockEditorRow
{
    public long ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string ItemType { get; set; } = "Item";

    public int StockQty { get; set; }

    public int TrackStock { get; set; }

    public string? LegacyId { get; set; }

    public long? CategoryId { get; set; }

    /// <summary>Display label: <c>CatalogBucket / CatalogSubCategory</c> (legacy join when unset).</summary>
    public string? CategoryName { get; set; }

    public string CatalogBucket { get; set; } = StockCatalogTaxonomy.BucketBar;

    public string CatalogSubCategory { get; set; } = "Drinks";

    public string StockMode { get; set; } = StockCatalogTaxonomy.StockModeTracked;

    public int NotGonnaOrderBack { get; set; }

    public int IncludeInWeeklyStockReport { get; set; }

    public int IsRunOutItem { get; set; }

    public int IsSharedItem { get; set; }

    public int IsOnSpecial { get; set; }

    public string? SpecialType { get; set; }

    public string? SpecialValue { get; set; }

    public string? SpecialLabel { get; set; }

    public string? SpecialAppliesTo { get; set; }

    public string? AlternateSkusJson { get; set; }

    public string? ItemDescription { get; set; }

    public string? ImagePath { get; set; }

    /// <summary>Legacy import JSON blob (image path fallback).</summary>
    public string? RawJson { get; set; }

    /// <summary>SQLite <c>Items.IsActive</c>; 0 means hidden from default catalog queries.</summary>
    public int IsActive { get; set; } = 1;

    /// <summary>Latest Bar <see cref="ItemPrices"/> row (nullable when none).</summary>
    public double? BarPrice { get; set; }

    /// <summary>Latest <c>PriceKind='Guest'</c> row when present.</summary>
    public double? GuestPrice { get; set; }

    /// <summary>Latest Pitstop-priced row when <c>PriceKind='Pitstop'</c> exists.</summary>
    public double? PitstopPrice { get; set; }

    /// <summary>Optional unit cost (SQLite <c>Items.CostPrice</c>).</summary>
    public double? CostPrice { get; set; }

    /// <summary>Optional per-item low-stock line; when null, UI uses default (e.g. 5).</summary>
    public int? LowStockThreshold { get; set; }

    /// <summary>When non-zero, bar POS prompts for unit price (numpad) each time the item is added.</summary>
    public int UsesOpenPrice { get; set; }

    /// <summary>When non-zero, item is sold from the catalog but not kept on hand—staff orders it in; sales do not decrement stock.</summary>
    public int OrderInMerchandise { get; set; }

    /// <summary>SQLite <c>Items.ShowInBar</c> when present (0 hides from bar catalog despite prices).</summary>
    public int ShowInBar { get; set; }

    /// <summary>SQLite <c>Items.ShowInPitstop</c> when present.</summary>
    public int ShowInPitstop { get; set; }

    /// <summary>Latest <c>PriceKind='BarSpecial'</c> when present.</summary>
    public double? BarSpecialPrice { get; set; }

    /// <summary>Latest <c>PriceKind='GuestSpecial'</c> when present.</summary>
    public double? GuestSpecialPrice { get; set; }

    /// <summary>Latest <c>PriceKind='PitstopSpecial'</c> when present.</summary>
    public double? PitstopSpecialPrice { get; set; }

    /// <summary>Target on-hand quantity (volunteer "Preferred Level").</summary>
    public int? PreferredStockLevel { get; set; }

    /// <summary>Critical low line ("Warn Me Below"). Falls back to <see cref="LowStockThreshold"/> when null.</summary>
    public int? WarnMeBelow { get; set; }

    /// <summary>Pack size for suggested buy (e.g. 24 cans).</summary>
    public int? PurchaseUnitQty { get; set; }

    /// <summary>When null, <see cref="IncludeInWeeklyStockReport"/> is used for shopping list visibility.</summary>
    public int? ShowOnShoppingList { get; set; }

    /// <summary>Last full stock-count session date (ISO-8601 text in SQLite).</summary>
    public string? LastStockCountDate { get; set; }
}

public sealed class StockPurchaseWrite
{
    public required long ItemId { get; init; }

    public required int PacksBought { get; init; }

    public required int ItemsPerPack { get; init; }

    public required int TotalItems { get; init; }

    public required decimal TotalPaid { get; init; }

    public required decimal CostEach { get; init; }

    public string? Notes { get; init; }
}

/// <summary>Full stock admin write (V2-aligned catalog + visibility + stock flags).</summary>
public sealed class StockItemAdminWrite
{
    public required long ItemId { get; init; }

    public required string Name { get; init; }

    public string? Sku { get; init; }

    public required int StockQty { get; init; }

    public required int TrackStock { get; init; }

    public string? ImagePath { get; init; }

    public int IsActive { get; init; } = 1;

    public string? StockMovementReference { get; init; }

    public double? CostPrice { get; init; }

    public int? LowStockThreshold { get; init; }

    public int UsesOpenPrice { get; init; }

    public int ShowInPitstop { get; init; }

    public int ShowInBar { get; init; }

    public int OrderInMerchandise { get; init; }

    public required string CatalogBucket { get; init; }

    public required string CatalogSubCategory { get; init; }

    public required string StockMode { get; init; }

    public int NotGonnaOrderBack { get; init; }

    public int IncludeInWeeklyStockReport { get; init; }

    public int? PreferredStockLevel { get; init; }

    public int? WarnMeBelow { get; init; }

    public int? PurchaseUnitQty { get; init; }

    /// <summary>Null leaves existing DB value unchanged on update.</summary>
    public int? ShowOnShoppingList { get; init; }

    public string? LastStockCountDate { get; init; }

    public int IsRunOutItem { get; init; }

    public int IsSharedItem { get; init; }

    public int IsOnSpecial { get; init; }

    public string? SpecialType { get; init; }

    public string? SpecialValue { get; init; }

    public string? SpecialLabel { get; init; }

    public string? SpecialAppliesTo { get; init; }

    public string? AlternateSkusJson { get; init; }

    public string? ItemDescription { get; init; }

    /// <summary>Legacy <c>Items.CategoryId</c>; null or omitted clears the link.</summary>
    public long? CategoryId { get; init; }
}

public interface IStockEditingService
{
    Task<IReadOnlyList<StockCategoryRow>> GetStockCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates an active category row (trimmed name). Throws if duplicate name (case-insensitive).</summary>
    Task<StockCategoryRow> CreateStockCategoryAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Renames an active category; duplicate names (case-insensitive) are rejected.</summary>
    Task UpdateStockCategoryAsync(long categoryId, string newName, CancellationToken cancellationToken = default);

    /// <summary>Moves a category earlier or later in <see cref="StockCategoryRow.SortOrder"/> (swap with neighbor).</summary>
    /// <param name="direction">-1 toward start of list, +1 toward end.</param>
    Task MoveStockCategoryAsync(long categoryId, int direction, CancellationToken cancellationToken = default);

    /// <summary>Soft-deactivates a category and clears <c>Items.CategoryId</c> for items that referenced it.</summary>
    Task SoftDeleteStockCategoryAsync(long categoryId, CancellationToken cancellationToken = default);

    /// <param name="includeInactive">When true, returns inactive (hidden) items too for admin recovery.</param>
    Task<IReadOnlyList<StockEditorRow>> GetStockRowsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Counts other items sharing the same trimmed SKU (case-insensitive), excluding <paramref name="excludeItemId"/> (use 0 for no exclusion).</summary>
    Task<int> CountOtherItemsWithSkuAsync(long excludeItemId, string sku, CancellationToken cancellationToken = default);

    /// <summary>Counts items whose primary SKU or JSON alternate list contains the code (case-insensitive).</summary>
    Task<int> CountOtherItemsMatchingScanCodeAsync(long excludeItemId, string scanCode, CancellationToken cancellationToken = default);

    /// <summary>Creates a minimal active item with a default Bar price row so it appears in the bar catalog.</summary>
    Task<long> CreateStockItemAsync(CancellationToken cancellationToken = default);

    /// <summary>Appends a new <see cref="ItemPrices"/> row for the given <paramref name="priceKind"/> (e.g. Bar, Pitstop, Guest, BarSpecial).</summary>
    Task UpsertLatestItemPriceAsync(long itemId, string priceKind, decimal price, CancellationToken cancellationToken = default);

    /// <summary>Updates quantity, stock tracking flag, and optional legacy category assignment (deprecated).</summary>
    Task UpdateStockRowAsync(
        long itemId,
        int newQty,
        int trackStock,
        long? categoryId,
        CancellationToken cancellationToken = default);

    /// <summary>Permanently removes the item and cascaded price/movement rows (SQLite FK).</summary>
    Task PermanentlyDeleteStockItemAsync(long itemId, CancellationToken cancellationToken = default);

    /// <summary>Admin stock screen: full item row including V2 catalog fields.</summary>
    Task UpdateItemAdminAsync(StockItemAdminWrite write, CancellationToken cancellationToken = default);

    /// <summary>Receive stock: increase qty, update cost, movement, and purchase row.</summary>
    Task ReceiveStockAsync(
        StockPurchaseWrite purchase,
        int newStockQty,
        double costEach,
        CancellationToken cancellationToken = default);

    /// <summary>Batch stock count: sets qty per item and writes count movements.</summary>
    Task ApplyStockCountAsync(
        IReadOnlyList<(long ItemId, int NewQty)> counts,
        IReadOnlyList<long>? countedItemIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>Latest stock-count timestamp across counted items, if any.</summary>
    Task<string?> GetLatestStockCountDateAsync(CancellationToken cancellationToken = default);

    Task InsertStockPurchaseAsync(StockPurchaseWrite purchase, CancellationToken cancellationToken = default);
}
