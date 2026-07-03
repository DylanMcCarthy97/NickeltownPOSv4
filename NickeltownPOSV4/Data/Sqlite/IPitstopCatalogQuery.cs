using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class PitstopCatalogProductRow
{
    public long ItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string CategoryName { get; set; } = "General";

    /// <summary>Bar / Pitstop / Shared bucket from stock catalog (set by pitstop catalog query).</summary>
    public string? CatalogBucket { get; set; }

    /// <summary>Food, Drinks, Merch, etc. (pitstop category chips filter on this).</summary>
    public string SubCategoryLabel { get; set; } = string.Empty;

    public double PitstopPrice { get; set; }

    /// <summary>Optional promotional Pitstop price when <c>PriceKind='PitstopSpecial'</c> exists.</summary>
    public double PitstopSpecialPrice { get; set; }

    public int IsOnSpecial { get; set; }

    public bool HasActiveSpecial => IsOnSpecial != 0 && PitstopSpecialPrice > 0.00001d;

    public double EffectivePitstopPrice => HasActiveSpecial ? PitstopSpecialPrice : PitstopPrice;

    public string PitstopPriceText => ((decimal)PitstopPrice).ToString("0.00", CultureInfo.InvariantCulture);

    public string EffectivePriceText => ((decimal)EffectivePitstopPrice).ToString("0.00", CultureInfo.InvariantCulture);

    public int StockQty { get; set; }

    public int TrackStock { get; set; }

    /// <summary>When non-zero, item is order-in (not kept on hand); retail sale skips stock decrement.</summary>
    public int OrderInMerchandise { get; set; }

    public string StockLabel => OrderInMerchandise != 0 ? "Order-in" : $"Stock {StockQty}";

    public string ItemType { get; set; } = "Item";

    public string? ImagePath { get; set; }

    /// <summary>True when an image path is configured (file may still be missing at runtime).</summary>
    public bool HasImagePath => !string.IsNullOrWhiteSpace(ImagePath);

    public string? Sku { get; set; }

    public bool HasSku => !string.IsNullOrWhiteSpace(Sku);

    public string SkuDisplay => string.IsNullOrWhiteSpace(Sku) ? string.Empty : $"SKU {Sku.Trim()}";

    public int UsesOpenPrice { get; set; }

    /// <summary>Optional per-item low-stock threshold from stock admin; UI falls back to 3 when null.</summary>
    public int? LowStockThreshold { get; set; }

    public int EffectiveLowStockThreshold => LowStockThreshold is > 0 ? LowStockThreshold.Value : 3;
}

public interface IPitstopCatalogQuery
{
    Task<IReadOnlyList<string>> GetPitstopCategoryNamesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PitstopCatalogProductRow>> GetPitstopProductsAsync(string? categoryName, CancellationToken cancellationToken = default);

    Task<PitstopCatalogProductRow?> FindBySkuAsync(string sku, CancellationToken cancellationToken = default);
}
