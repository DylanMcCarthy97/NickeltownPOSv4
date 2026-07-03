using System;
using System.Globalization;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.AddDrinks;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Editable fields loaded from a stock list row (stock management detail pane).</summary>
internal sealed class StockItemDetailSnapshot
{
    public long ItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string ItemType { get; init; } = "Item";

    public string Sku { get; init; } = string.Empty;

    public string ImagePath { get; init; } = string.Empty;

    public string StockText { get; init; } = "0";

    public bool TrackStock { get; init; }

    public bool IsActive { get; init; } = true;

    public string CatalogBucket { get; init; } = StockCatalogTaxonomy.BucketBar;

    public string CatalogSubCategory { get; init; } = "Drinks";

    public string StockMode { get; init; } = StockCatalogTaxonomy.StockModeTracked;

    public string CatalogDisplay { get; init; } = string.Empty;

    public string BarPriceText { get; init; } = string.Empty;

    public string PitstopPriceText { get; init; } = string.Empty;

    public string GuestPriceText { get; init; } = string.Empty;

    public string BarSpecialText { get; init; } = string.Empty;

    public string GuestSpecialText { get; init; } = string.Empty;

    public string PitstopSpecialText { get; init; } = string.Empty;

    public string AlternateSkusText { get; init; } = string.Empty;

    public string ItemDescription { get; init; } = string.Empty;

    public bool IsOnSpecial { get; init; }

    public string SpecialType { get; init; } = CatalogSpecialValueResolver.FixedPrice;

    public string SpecialValueText { get; init; } = string.Empty;

    public string SpecialLabel { get; init; } = string.Empty;

    public string SpecialAppliesToMode { get; init; } = "Bar";

    public string CostPriceText { get; init; } = string.Empty;

    public string LowStockThresholdText { get; init; } = string.Empty;

    public bool UsesOpenPrice { get; init; }

    public bool ShowInBar { get; init; }

    public bool ShowInPitstop { get; init; }

    public bool OrderInMerchandise { get; init; }

    public bool IsShotMixerItem => ShotMixerCatalog.IsShotMixerItem(Name, ItemType);

    public string ShotMixerSpiritsText =>
        IsShotMixerItem
            ? ShotMixerSpiritsSerializer.ToDisplayTextFromDescription(ItemDescription)
            : string.Empty;

    public static StockItemDetailSnapshot FromRow(StockEditorRow src)
    {
        var bucket = StockCatalogTaxonomy.NormalizeBucket(src.CatalogBucket);
        var sub = StockCatalogTaxonomy.NormalizeSubCategory(bucket, src.CatalogSubCategory);
        var stockMode = string.IsNullOrWhiteSpace(src.StockMode)
            ? StockCatalogTaxonomy.StockModeFromLegacyFlags(src.TrackStock, src.OrderInMerchandise, src.NotGonnaOrderBack)
            : StockCatalogTaxonomy.NormalizeStockMode(src.StockMode);

        return new StockItemDetailSnapshot
        {
            ItemId = src.ItemId,
            Name = src.Name,
            ItemType = string.IsNullOrWhiteSpace(src.ItemType) ? "Item" : src.ItemType.Trim(),
            Sku = src.Sku ?? string.Empty,
            ImagePath = src.ImagePath ?? string.Empty,
            StockText = src.StockQty.ToString(CultureInfo.InvariantCulture),
            TrackStock = src.TrackStock != 0,
            IsActive = src.IsActive != 0,
            CatalogBucket = bucket,
            CatalogSubCategory = sub,
            StockMode = stockMode,
            CatalogDisplay = StockCatalogTaxonomy.CatalogDisplayName(bucket, sub),
            BarPriceText = StockMoneyInputParser.FormatOptionalMoney(src.BarPrice),
            PitstopPriceText = StockMoneyInputParser.FormatOptionalMoney(src.PitstopPrice),
            GuestPriceText = StockMoneyInputParser.FormatOptionalMoney(src.GuestPrice),
            BarSpecialText = StockMoneyInputParser.FormatOptionalMoney(src.BarSpecialPrice),
            GuestSpecialText = StockMoneyInputParser.FormatOptionalMoney(src.GuestSpecialPrice),
            PitstopSpecialText = StockMoneyInputParser.FormatOptionalMoney(src.PitstopSpecialPrice),
            IsOnSpecial = src.IsOnSpecial != 0,
            SpecialType = ResolveSpecialTypeForUi(src),
            SpecialValueText = ResolveSpecialValueForUi(src),
            SpecialLabel = src.SpecialLabel ?? string.Empty,
            SpecialAppliesToMode = ResolveSpecialAppliesTo(src),
            AlternateSkusText = string.Join(Environment.NewLine, StockAlternateSkuHelper.ParseAlternateSkuList(src.AlternateSkusJson)),
            ItemDescription = src.ItemDescription ?? string.Empty,
            CostPriceText = StockMoneyInputParser.FormatOptionalMoney(src.CostPrice),
            LowStockThresholdText = src.LowStockThreshold?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            UsesOpenPrice = src.UsesOpenPrice != 0,
            ShowInBar = src.ShowInBar != 0,
            ShowInPitstop = src.ShowInPitstop != 0,
            OrderInMerchandise = src.OrderInMerchandise != 0,
        };
    }

    private static string ResolveSpecialTypeForUi(StockEditorRow src)
    {
        if (!string.IsNullOrWhiteSpace(src.SpecialType))
        {
            return CatalogSpecialValueResolver.NormalizeType(src.SpecialType);
        }

        return CatalogSpecialValueResolver.FixedPrice;
    }

    private static string ResolveSpecialValueForUi(StockEditorRow src)
    {
        if (CatalogSpecialValueResolver.TryFormatStoredValueForUi(src.SpecialType, src.SpecialValue, out var stored))
        {
            return stored;
        }

        if (src.IsOnSpecial == 0)
        {
            return string.Empty;
        }

        var bar = StockMoneyInputParser.FormatOptionalMoney(src.BarSpecialPrice);
        if (!string.IsNullOrEmpty(bar))
        {
            return bar;
        }

        return StockMoneyInputParser.FormatOptionalMoney(src.PitstopSpecialPrice);
    }

    private static string ResolveSpecialAppliesTo(StockEditorRow src)
    {
        var mode = (src.SpecialAppliesTo ?? string.Empty).Trim();
        if (string.Equals(mode, "Pitstop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "Both", StringComparison.OrdinalIgnoreCase))
        {
            return mode;
        }

        if (src.IsOnSpecial != 0
            && src.PitstopSpecialPrice is > 0.00001d
            && (src.BarSpecialPrice is null or <= 0.00001d))
        {
            return "Pitstop";
        }

        if (src.IsOnSpecial != 0
            && src.PitstopSpecialPrice is > 0.00001d
            && src.BarSpecialPrice is > 0.00001d)
        {
            return "Both";
        }

        return "Bar";
    }
}
