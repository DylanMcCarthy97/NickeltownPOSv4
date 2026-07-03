using System;
using System.Collections.Generic;
using System.Globalization;

namespace NickeltownPOSV4.ViewModels;

public sealed class DrinkCardItem : ObservableViewModel
{
    private int _cartQuantity;
    private bool _isFavorite;

    public DrinkCardItem(
        long itemId,
        string name,
        string categoryName,
        decimal unitPrice,
        int stockQty,
        int trackStock,
        string? imagePath,
        string? sku,
        bool usesOpenPrice,
        bool orderInMerchandise = false,
        decimal pitstopShelfPrice = 0m,
        string itemType = "Item",
        bool isFavorite = false,
        IReadOnlyList<string>? alternateSkus = null,
        bool showSpecialPricing = false,
        decimal regularUnitPrice = 0m)
    {
        ItemId = itemId;
        Name = name;
        CategoryName = categoryName;
        UnitPrice = unitPrice;
        ShowSpecialPricing = showSpecialPricing;
        RegularUnitPrice = regularUnitPrice;
        PitstopShelfPrice = pitstopShelfPrice;
        StockQty = stockQty;
        TrackStock = trackStock;
        UsesOpenPrice = usesOpenPrice;
        OrderInMerchandise = orderInMerchandise;
        ImagePath = imagePath;
        Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();
        ItemType = string.IsNullOrWhiteSpace(itemType) ? "Item" : itemType.Trim();
        ImageUri = BuildImageUri(imagePath);
        HasImage = ImageUri is not null;
        StockLabel = orderInMerchandise
            ? "Order-in (not stocked)"
            : (trackStock != 0 ? $"Stock {stockQty}" : "—");
        _cartQuantity = 0;
        _isFavorite = isFavorite;
        AlternateSkus = alternateSkus ?? Array.Empty<string>();
    }

    /// <summary>SQLite <c>Items.ItemType</c> (e.g. Shot) for mixer prompt heuristics.</summary>
    public string ItemType { get; }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    /// <summary>When true, staff enters sale price on the touch numpad when adding to the cart.</summary>
    public bool UsesOpenPrice { get; }

    /// <summary>Merchandise ordered in as needed; may be sold when on-hand is zero.</summary>
    public bool OrderInMerchandise { get; }

    /// <summary>Units of this SKU currently on the cart (for subtle in-grid highlight).</summary>
    public int CartQuantity
    {
        get => _cartQuantity;
        set
        {
            if (SetProperty(ref _cartQuantity, value))
            {
                OnPropertyChanged(nameof(IsInCart));
                OnPropertyChanged(nameof(ShowCartQtyBadge));
                OnPropertyChanged(nameof(CartQtyBadgeText));
            }
        }
    }

    public bool IsInCart => _cartQuantity > 0;

    public bool ShowCartQtyBadge => _cartQuantity > 0;

    /// <summary>"✓ Added" for the first unit; "×N" for additional units.</summary>
    public string CartQtyBadgeText
    {
        get
        {
            if (_cartQuantity <= 0)
            {
                return string.Empty;
            }

            if (_cartQuantity == 1)
            {
                return "\u2713 Added";
            }

            if (_cartQuantity > 99)
            {
                return "\u00D799+";
            }

            return $"\u00D7{_cartQuantity.ToString(CultureInfo.InvariantCulture)}";
        }
    }

    public bool ShowOutOfStockOverlay => !CanAddFromCatalog;

    /// <summary>Fades the card when the item cannot be added (out of stock).</summary>
    public double CardOpacity => CanAddFromCatalog ? 1.0 : 0.55;

    /// <summary>Stock text shown under the price (hidden for OOS, untracked, and zero-on-hand items).</summary>
    public string CleanStockLabel
    {
        get
        {
            if (!CanAddFromCatalog)
            {
                return string.Empty;
            }

            if (OrderInMerchandise)
            {
                return "Order-in";
            }

            if (TrackStock != 0 && StockQty > 0)
            {
                return $"Stock {StockQty}";
            }

            return string.Empty;
        }
    }

    public bool ShowCleanStockLabel => !string.IsNullOrEmpty(CleanStockLabel);

    public bool IsOrderInMerchandise => OrderInMerchandise;

    public long ItemId { get; }

    public string Name { get; }

    /// <summary>SKU / barcode from SQLite; used for wedge scanners.</summary>
    public string? Sku { get; }

    /// <summary>Additional scan codes (from Items.AlternateSkusJson).</summary>
    public IReadOnlyList<string> AlternateSkus { get; }

    public string CategoryName { get; }

    public decimal UnitPrice { get; }

    /// <summary>Pre-special shelf price when <see cref="ShowSpecialPricing"/> is true.</summary>
    public decimal RegularUnitPrice { get; }

    /// <summary>When true, the card shows struck-through regular price and red special price.</summary>
    public bool ShowSpecialPricing { get; }

    /// <summary>Latest Pitstop retail unit price (same SKU as bar); used for hints when bar is $0.</summary>
    public decimal PitstopShelfPrice { get; }

    public string PriceText =>
        UsesOpenPrice ? "Open price" : $"${UnitPrice:0.00}";

    public string RegularPriceText => $"${RegularUnitPrice:0.00}";

    public string SpecialPriceText => $"${UnitPrice:0.00}";

    public string PriceStockLine => $"{PriceText}  ·  {StockLabel}";

    /// <summary>True when stock is tracked and on-hand is zero (visual hint only; sale rules unchanged).</summary>
    public bool ShowZeroStockHint => TrackStock != 0 && StockQty <= 0 && !OrderInMerchandise;

    /// <summary>Reserves footer space on every card so grid rows stay equal height.</summary>
    public double ZeroStockHintOpacity => ShowZeroStockHint ? 1.0 : 0.0;

    /// <summary>Grid tap / scan may add when not tracked, in stock, or order-in merchandise.</summary>
    public bool CanAddFromCatalog => OrderInMerchandise || TrackStock == 0 || StockQty > 0;

    /// <summary>Highlights order-in items on the catalog card.</summary>
    public bool ShowOrderInMerchandiseHint => OrderInMerchandise;

    public int StockQty { get; }

    public int TrackStock { get; }

    public string StockLabel { get; }

    public string? ImagePath { get; }

    public Uri? ImageUri { get; }

    public bool HasImage { get; }

    private static Uri? BuildImageUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var t = path.Trim();
        try
        {
            if (t.StartsWith("ms-appx:", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("ms-appdata:", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(t, UriKind.Absolute);
            }

            if (System.IO.Path.IsPathRooted(t))
            {
                return new Uri(t, UriKind.Absolute);
            }

            if (Uri.TryCreate(t, UriKind.Absolute, out var absolute) &&
                (absolute.Scheme == Uri.UriSchemeFile || absolute.Scheme == "ms-appdata"))
            {
                return absolute;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
