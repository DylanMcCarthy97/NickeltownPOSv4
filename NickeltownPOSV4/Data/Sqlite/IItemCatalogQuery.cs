using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Read-only catalog for POS items (SQLite), Bar price mode.</summary>
public interface IItemCatalogQuery
{
    /// <summary>Distinct category names for active, priced products (Bar price kind).</summary>
    Task<IReadOnlyList<string>> GetBarCategoryNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Active items for bar POS: Bar-priced (including $0), open-price at bar, or Pitstop-priced when <c>ShowInBar</c> is on.</summary>
    Task<IReadOnlyList<BarCatalogProductRow>> GetBarProductsAsync(string? categoryName, CancellationToken cancellationToken = default);
}

public sealed class BarCatalogProductRow
{
    public long ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CategoryName { get; set; } = "General";

    public string CatalogBucket { get; set; } = StockCatalogTaxonomy.BucketBar;

    public string CatalogSubCategory { get; set; } = "Drinks";

    public double BarPrice { get; set; }

    public double GuestPrice { get; set; }

    /// <summary>Latest Pitstop shelf price when set (for bar cards: show “retail” when bar is complimentary).</summary>
    public double PitstopPrice { get; set; }

    public double BarSpecialPrice { get; set; }

    public double GuestSpecialPrice { get; set; }

    public int IsOnSpecial { get; set; }

    public int StockQty { get; set; }

    public int TrackStock { get; set; }

    public string ItemType { get; set; } = "Item";

    public string? ImagePath { get; set; }

    /// <summary>SKU / primary barcode for wedge scanners (matches Items.Sku).</summary>
    public string? Sku { get; set; }

    public string? AlternateSkusJson { get; set; }

    public int UsesOpenPrice { get; set; }

    /// <summary>When non-zero, item is order-in merchandise (not kept on hand); bar add-drink allows sale at 0 qty.</summary>
    public int OrderInMerchandise { get; set; }
}
