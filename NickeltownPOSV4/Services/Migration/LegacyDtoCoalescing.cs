using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.Services.Migration;

/// <summary>
/// WinForms V2 exports vary property names; merge root-level aliases after <see cref="JsonSerializer"/> deserialization.
/// </summary>
internal static class LegacyDtoCoalescing
{
    public static void ApplyLooseCategoryFields(LegacyCategoryDto dto, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var p in element.EnumerateObject())
        {
            var n = p.Name.ToLowerInvariant();
            switch (n)
            {
                case "label":
                case "title":
                case "displayname":
                    dto.DisplayName ??= ReadString(p.Value);
                    break;
                case "sort":
                case "order":
                case "ordinal":
                    dto.SortOrder ??= ReadInt(p.Value);
                    break;
                case "enabled":
                case "visible":
                case "isactive":
                case "is_active":
                    dto.Active ??= ReadBool(p.Value);
                    dto.IsActive ??= ReadBool(p.Value);
                    break;
                case "code":
                case "slug":
                    dto.Key ??= ReadString(p.Value);
                    break;
            }
        }
    }

    public static void ApplyLooseItemFields(LegacyItemDto dto, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var p in element.EnumerateObject())
        {
            var n = p.Name.ToLowerInvariant();
            switch (n)
            {
                case "sku":
                case "productsku":
                case "itemcode":
                case "itemnumber":
                    dto.Sku ??= ReadString(p.Value);
                    break;
                case "productcode":
                    dto.ProductCode ??= ReadString(p.Value);
                    break;
                case "barcode":
                case "upc":
                case "ean":
                case "gtin":
                    dto.Barcode ??= ReadString(p.Value);
                    break;
                case "barcodes":
                case "alternatebarcodes":
                case "alternate_barcodes":
                    if (dto.Barcodes is null && p.Value.ValueKind == JsonValueKind.Array)
                    {
                        var codes = new List<string>();
                        foreach (var el in p.Value.EnumerateArray())
                        {
                            var s = ReadString(el);
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                codes.Add(s.Trim());
                            }
                        }

                        if (codes.Count > 0)
                        {
                            dto.Barcodes = codes;
                        }
                    }

                    break;
                case "categoryid":
                case "category_id":
                case "catid":
                case "departmentid":
                    dto.CategoryId ??= ReadString(p.Value);
                    break;
                case "categoryname":
                case "department":
                case "group":
                    dto.Category ??= ReadString(p.Value);
                    break;
                case "quantity":
                case "qty":
                case "onhand":
                case "on_hand":
                case "amount":
                case "count":
                case "stockqty":
                case "stockcount":
                case "stock_quantity":
                case "inventoryqty":
                    if (dto.Stock is null && dto.StockCount is null)
                    {
                        var q = ReadInt(p.Value);
                        dto.Stock = q;
                        dto.StockCount = q;
                    }

                    break;
                case "trackstock":
                case "track_inventory":
                case "trackinventory":
                case "inventorytracked":
                    dto.TrackStock ??= ReadBool(p.Value);
                    break;
                case "imagepath":
                case "image":
                case "imageurl":
                case "photo":
                case "thumbnail":
                case "picture":
                    dto.ImagePath ??= ReadString(p.Value);
                    break;
                case "subcategory":
                case "sub_category":
                    dto.SubCategory ??= ReadString(p.Value);
                    break;
                case "pitstopprice":
                case "pitstop_price":
                    dto.PitstopPrice ??= ReadDecimal(p.Value);
                    break;
                case "cost":
                case "costprice":
                    dto.Cost ??= ReadDecimal(p.Value);
                    break;
                case "description":
                case "itemdescription":
                    dto.Description ??= ReadString(p.Value);
                    break;
                case "lowstockthreshold":
                case "low_stock_threshold":
                    dto.LowStockThreshold ??= ReadInt(p.Value);
                    break;
                case "showinbar":
                case "show_in_bar":
                    dto.ShowInBar ??= ReadBool(p.Value);
                    break;
                case "showinpitstop":
                case "show_in_pitstop":
                    dto.ShowInPitstop ??= ReadBool(p.Value);
                    break;
                case "notgonnaorderback":
                case "not_gonna_order_back":
                    dto.NotGonnaOrderBack ??= ReadBool(p.Value);
                    break;
                case "isonspecial":
                case "is_on_special":
                    dto.IsOnSpecial ??= ReadBool(p.Value);
                    break;
                case "specialprice":
                case "special_price":
                    dto.SpecialPrice ??= ReadDecimal(p.Value);
                    break;
                case "specialpitstopprice":
                case "special_pitstop_price":
                    dto.SpecialPitstopPrice ??= ReadDecimal(p.Value);
                    break;
                case "isshareditem":
                case "is_shared_item":
                    dto.IsSharedItem ??= ReadBool(p.Value);
                    break;
                case "isactive":
                case "is_active":
                    dto.IsActive ??= ReadBool(p.Value);
                    break;
                case "orderin":
                case "order_in":
                case "orderinmerchandise":
                    dto.OrderIn ??= ReadBool(p.Value);
                    break;
                case "stockmode":
                    dto.StockMode ??= ReadString(p.Value);
                    break;
                case "disablestocktracking":
                case "disable_stock_tracking":
                    if (ReadBool(p.Value) == true)
                    {
                        dto.DisableStockTracking = true;
                    }

                    break;
                case "guestprice":
                case "guest_price":
                    dto.GuestPrice ??= ReadDecimal(p.Value);
                    break;
                case "specialguestprice":
                case "special_guest_price":
                    dto.SpecialGuestPrice ??= ReadDecimal(p.Value);
                    break;
                case "parlevel":
                case "par_level":
                    dto.ParLevel ??= ReadInt(p.Value);
                    break;
                case "sourcesystem":
                case "source_system":
                    dto.SourceSystem ??= ReadString(p.Value);
                    break;
                case "price":
                    if (dto.Price is null)
                    {
                        dto.Price ??= ReadDecimal(p.Value);
                    }

                    break;
            }
        }
    }

    public static void ApplyLooseDrinkFields(LegacyDrinkDto dto, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var p in element.EnumerateObject())
        {
            var n = p.Name.ToLowerInvariant();
            switch (n)
            {
                case "sku":
                case "productsku":
                case "productcode":
                    dto.Sku ??= ReadString(p.Value);
                    break;
                case "barcode":
                case "upc":
                case "ean":
                    dto.Barcode ??= ReadString(p.Value);
                    break;
                case "category":
                case "categoryname":
                case "group":
                case "department":
                    dto.Category ??= ReadString(p.Value);
                    break;
                case "categoryid":
                case "category_id":
                case "catid":
                    dto.CategoryId ??= ReadString(p.Value);
                    break;
                case "quantity":
                case "qty":
                case "onhand":
                case "on_hand":
                case "amount":
                case "count":
                case "stockqty":
                    if (dto.Stock is null && dto.Quantity is null)
                    {
                        var q = ReadInt(p.Value);
                        if (q is not null)
                        {
                            dto.Stock = q;
                        }
                    }

                    break;
                case "trackstock":
                case "track_inventory":
                case "trackinventory":
                    dto.TrackStock ??= ReadBool(p.Value);
                    break;
                case "imagepath":
                case "image":
                case "imageurl":
                case "photo":
                case "thumbnail":
                    dto.ImagePath ??= ReadString(p.Value);
                    break;
            }
        }
    }

    public static string? ResolveItemSku(LegacyItemDto dto)
    {
        var fromList = dto.Barcodes is { Count: > 0 } ? dto.Barcodes[0]?.Trim() : null;
        return FirstNonEmpty(dto.Sku, dto.Barcode, dto.ProductCode, fromList);
    }

    public static string? ResolveDrinkSku(LegacyDrinkDto dto) =>
        FirstNonEmpty(dto.Sku, dto.Barcode, dto.Name);

    public static int ResolveItemStockQty(LegacyItemDto dto) =>
        dto.StockCount ?? dto.Stock ?? dto.Quantity ?? dto.OnHand ?? dto.Amount ?? 0;

    public static int ResolveDrinkStockQty(LegacyDrinkDto dto) =>
        dto.Stock ?? dto.Quantity ?? dto.OnHand ?? dto.Amount ?? 0;

    public static int ResolveItemTrackStock(LegacyItemDto dto)
    {
        if (dto.DisableStockTracking == true || dto.OrderIn == true)
        {
            return 0;
        }

        return dto.TrackStock == true || dto.TrackInventory == true ? 1 :
            dto.TrackStock == false || dto.TrackInventory == false ? 0 : 1;
    }

    public static int ResolveDrinkTrackStock(LegacyDrinkDto dto) =>
        dto.TrackStock == true || dto.TrackInventory == true ? 1 :
        dto.TrackStock == false || dto.TrackInventory == false ? 0 : 1;

    /// <summary>POSBarV2: fill primary sku from first alternate when main barcode empty.</summary>
    public static void FinalizePosBarStockItem(LegacyItemDto dto)
    {
        if (dto.Barcodes is { Count: > 0 })
        {
            var first = dto.Barcodes[0]?.Trim();
            if (!string.IsNullOrEmpty(first))
            {
                dto.Barcode ??= first;
                dto.Sku ??= first;
            }
        }
    }

    /// <summary>Legacy <c>Categories</c> name link (bucket + sub) without mutating import bucket fields.</summary>
    public static string? LegacyCategoryLinkName(LegacyItemDto dto)
    {
        var sub = dto.SubCategory?.Trim();
        var cat = dto.Category?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(sub))
        {
            return string.IsNullOrEmpty(cat) ? sub : $"{cat} / {sub}";
        }

        return string.IsNullOrEmpty(cat) ? null : cat;
    }

    public static string? ResolveAlternateSkusJson(LegacyItemDto dto)
    {
        var primary = ResolveItemSku(dto);
        var extras = new List<string>();

        if (dto.Barcodes is not null)
        {
            foreach (var code in dto.Barcodes)
            {
                var t = code?.Trim();
                if (string.IsNullOrEmpty(t))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(primary)
                    && string.Equals(t, primary, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (extras.Any(e => string.Equals(e, t, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                extras.Add(t);
            }
        }

        return extras.Count == 0 ? null : JsonSerializer.Serialize(extras);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v.Trim();
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement v) =>
        v.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(v.GetString()) ? null : v.GetString()!.Trim(),
            JsonValueKind.Number => v.GetRawText().Trim(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null,
        };

    private static int? ReadInt(JsonElement v)
    {
        try
        {
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.TryGetInt32(out var i) ? i : (int)Math.Truncate(v.GetDouble()),
                JsonValueKind.String => int.TryParse(
                    v.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var p)
                    ? p
                    : null,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool? ReadBool(JsonElement v) =>
        v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(v.GetString(), out var b) ? b : null,
            JsonValueKind.Number => v.TryGetInt32(out var n) ? n != 0 : null,
            _ => null,
        };

    private static decimal? ReadDecimal(JsonElement v)
    {
        try
        {
            return v.ValueKind switch
            {
                JsonValueKind.Number => v.GetDecimal(),
                JsonValueKind.String => decimal.TryParse(
                    v.GetString(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var p)
                    ? p
                    : null,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Maps POSBar <c>stock_items.json</c> rows onto V4 SQLite item columns and price kinds.</summary>
internal static class LegacyItemImportMapper
{
    internal sealed class ItemImportRow
    {
        public required string LegacyId { get; init; }
        public required string LegacyKey { get; init; }
        public required string Name { get; init; }
        public string? Sku { get; init; }
        public string? AlternateSkusJson { get; init; }
        public string? LegacyCategoryLinkName { get; init; }
        public required string CatalogBucket { get; init; }
        public required string CatalogSubCategory { get; init; }
        public required string StockMode { get; init; }
        public int StockQty { get; init; }
        public int TrackStock { get; init; }
        public int OrderInMerchandise { get; init; }
        public int NotGonnaOrderBack { get; init; }
        public int IncludeInWeeklyStockReport { get; init; }
        public int IsRunOutItem { get; init; }
        public int ShowInBar { get; init; }
        public int ShowInPitstop { get; init; }
        public int IsSharedItem { get; init; }
        public int IsActive { get; init; }
        public int UsesOpenPrice { get; init; }
        public int IsOnSpecial { get; init; }
        public string? SpecialType { get; init; }
        public string? SpecialValue { get; init; }
        public decimal? CostPrice { get; init; }
        public int? LowStockThreshold { get; init; }
        public string? ItemDescription { get; init; }
        public string? ImagePath { get; init; }
        public decimal? BarPrice { get; init; }
        public decimal? GuestPrice { get; init; }
        public decimal? PitstopPrice { get; init; }
        public decimal? BarSpecialPrice { get; init; }
        public decimal? GuestSpecialPrice { get; init; }
        public decimal? PitstopSpecialPrice { get; init; }
    }

    public static ItemImportRow Map(LegacyItemDto dto, string legacyId)
    {
        var bucket = MapCatalogBucket(dto);
        var sub = StockCatalogTaxonomy.NormalizeSubCategory(bucket, dto.SubCategory);

        var trackForMode = dto.DisableStockTracking == true ? 0 :
            dto.TrackStock == false || dto.TrackInventory == false ? 0 : 1;
        var orderInFlag = dto.OrderIn == true ? 1 : 0;
        var notGonnaFlag = dto.NotGonnaOrderBack == true ? 1 : 0;
        var stockMode = !string.IsNullOrWhiteSpace(dto.StockMode)
            ? StockCatalogTaxonomy.NormalizeStockMode(dto.StockMode)
            : StockCatalogTaxonomy.StockModeFromLegacyFlags(trackForMode, orderInFlag, notGonnaFlag);
        StockCatalogTaxonomy.ApplyStockModeToFlags(
            stockMode,
            out var trackStock,
            out var orderInMerch,
            out var notGonna,
            out var includeWeekly,
            out var runOut);

        var showBar = dto.ShowInBar == false ? 0 : 1;
        var showPit = dto.ShowInPitstop switch
        {
            false => 0,
            true => 1,
            _ => dto.PitstopPrice is { } pitHint && pitHint > 0m ? 1 : 0,
        };
        var isActive = dto.IsActive == false ? 0 : 1;
        var isShared = (dto.IsSharedItem == true || bucket == StockCatalogTaxonomy.BucketShared) ? 1 : 0;

        var barPrice = dto.Price;
        var usesOpen = barPrice is 0m && showBar != 0 ? 1 : 0;

        var isOnSpecial = dto.IsOnSpecial == true ? 1 : 0;
        string? specialType = null;
        string? specialValue = null;
        decimal? barSpecial = null;
        decimal? guestSpecial = null;
        decimal? pitSpecial = null;

        if (isOnSpecial != 0)
        {
            if (dto.SpecialPrice is { } sp && sp > 0m)
            {
                barSpecial = sp;
                specialType = CatalogSpecialValueResolver.FixedPrice;
                specialValue = sp.ToString("0.00", CultureInfo.InvariantCulture);
            }

            if (dto.SpecialGuestPrice is { } sg && sg > 0m)
            {
                guestSpecial = sg;
            }

            if (dto.SpecialPitstopPrice is { } psp && psp > 0m)
            {
                pitSpecial = psp;
            }
        }

        var meta = new StockItemMetadataSerializer.StockItemMetadata
        {
            Notes = string.IsNullOrWhiteSpace(dto.Description) ? string.Empty : dto.Description.Trim(),
            ParLevel = dto.ParLevel,
            SourceSystem = dto.SourceSystem ?? string.Empty,
        };
        var itemDescription = StockItemMetadataSerializer.ToStorageJson(meta, includeSpirits: false);
        if (string.IsNullOrEmpty(itemDescription) && !string.IsNullOrWhiteSpace(meta.Notes))
        {
            itemDescription = meta.Notes;
        }

        return new ItemImportRow
        {
            LegacyId = legacyId,
            LegacyKey = legacyId,
            Name = dto.Name ?? "Item",
            Sku = LegacyDtoCoalescing.ResolveItemSku(dto),
            AlternateSkusJson = LegacyDtoCoalescing.ResolveAlternateSkusJson(dto),
            LegacyCategoryLinkName = LegacyDtoCoalescing.LegacyCategoryLinkName(dto),
            CatalogBucket = bucket,
            CatalogSubCategory = sub,
            StockMode = stockMode,
            StockQty = LegacyDtoCoalescing.ResolveItemStockQty(dto),
            TrackStock = trackStock,
            OrderInMerchandise = orderInMerch,
            NotGonnaOrderBack = notGonna,
            IncludeInWeeklyStockReport = includeWeekly,
            IsRunOutItem = runOut,
            ShowInBar = showBar,
            ShowInPitstop = showPit,
            IsSharedItem = isShared,
            IsActive = isActive,
            UsesOpenPrice = usesOpen,
            IsOnSpecial = isOnSpecial,
            SpecialType = specialType,
            SpecialValue = specialValue,
            CostPrice = dto.Cost is { } c && c > 0m ? c : null,
            LowStockThreshold = dto.LowStockThreshold,
            ItemDescription = string.IsNullOrWhiteSpace(itemDescription) ? null : itemDescription,
            ImagePath = string.IsNullOrWhiteSpace(dto.ImagePath) ? null : dto.ImagePath.Trim(),
            BarPrice = barPrice,
            GuestPrice = dto.GuestPrice is { } guest && guest > 0m ? guest : null,
            PitstopPrice = dto.PitstopPrice,
            BarSpecialPrice = barSpecial,
            GuestSpecialPrice = guestSpecial,
            PitstopSpecialPrice = pitSpecial,
        };
    }

    private static string MapCatalogBucket(LegacyItemDto dto)
    {
        if (dto.IsSharedItem == true)
        {
            return StockCatalogTaxonomy.BucketShared;
        }

        var cat = (dto.Category ?? string.Empty).Trim();
        if (cat.Equals(StockCatalogTaxonomy.BucketPitstop, StringComparison.OrdinalIgnoreCase)
            || cat.Equals("Pit Stop", StringComparison.OrdinalIgnoreCase))
        {
            return StockCatalogTaxonomy.BucketPitstop;
        }

        if (cat.Equals(StockCatalogTaxonomy.BucketShared, StringComparison.OrdinalIgnoreCase))
        {
            return StockCatalogTaxonomy.BucketShared;
        }

        return StockCatalogTaxonomy.BucketBar;
    }
}
