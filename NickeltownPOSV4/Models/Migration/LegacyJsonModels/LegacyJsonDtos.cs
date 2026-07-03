using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NickeltownPOSV4.Models.Migration.LegacyJsonModels;

/// <summary>
/// DTOs for WinForms V2 JSON. Property names are best-effort; <see cref="JsonExtensionData"/> preserves unknown fields for forward-compatible mapping.
/// </summary>
public sealed class LegacyDrinkDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Sku { get; set; }

    /// <summary>Alternate product codes from some exports (merged with <see cref="Sku"/> during migration).</summary>
    public string? Barcode { get; set; }

    public string? Category { get; set; }

    public string? CategoryId { get; set; }

    public decimal? Price { get; set; }

    public int? Stock { get; set; }

    public int? Quantity { get; set; }

    public int? OnHand { get; set; }

    public int? Amount { get; set; }

    public bool? TrackStock { get; set; }

    public bool? TrackInventory { get; set; }

    public string? ImagePath { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class LegacyItemDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    /// <summary>POSBarV2 <c>stock_items.json</c>: top-level group (e.g. Bar, Pitstop).</summary>
    public string? Category { get; set; }

    /// <summary>POSBarV2: Beer, Wine, Food, etc.</summary>
    public string? SubCategory { get; set; }

    /// <summary>Legacy category row id / key when the export references categories by id.</summary>
    public string? CategoryId { get; set; }

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public List<string>? Barcodes { get; set; }

    public string? ProductCode { get; set; }

    public decimal? Price { get; set; }

    public decimal? GuestPrice { get; set; }

    public decimal? PitstopPrice { get; set; }

    public decimal? Cost { get; set; }

    public int? LowStockThreshold { get; set; }

    public int? ParLevel { get; set; }

    /// <summary>POSBarV2 explicit stock mode string when present.</summary>
    public string? StockMode { get; set; }

    public string? SourceSystem { get; set; }

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public bool? ShowInBar { get; set; }

    public bool? ShowInPitstop { get; set; }

    public bool? NotGonnaOrderBack { get; set; }

    public bool? IsOnSpecial { get; set; }

    public decimal? SpecialPrice { get; set; }

    public decimal? SpecialGuestPrice { get; set; }

    public decimal? SpecialPitstopPrice { get; set; }

    public bool? IsSharedItem { get; set; }

    /// <summary>POSBarV2 primary on-hand field name.</summary>
    public int? StockCount { get; set; }

    public int? Stock { get; set; }

    public int? Quantity { get; set; }

    public int? OnHand { get; set; }

    public int? Amount { get; set; }

    public bool? TrackStock { get; set; }

    public bool? TrackInventory { get; set; }

    /// <summary>POSBarV2: when true, maps to SQLite <c>TrackStock = 0</c>.</summary>
    public bool? DisableStockTracking { get; set; }

    /// <summary>POSBarV2: order-in items do not use normal stock adjustments.</summary>
    public bool? OrderIn { get; set; }

    public string? ImagePath { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class LegacyCategoryDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    /// <summary>POSBar unified_categories.json display label (e.g. Food).</summary>
    public string? DisplayName { get; set; }

    public int? SortOrder { get; set; }

    public bool? Active { get; set; }

    public bool? IsActive { get; set; }

    public bool? ShowInBar { get; set; }

    public bool? ShowInPitstop { get; set; }

    public string? Key { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class LegacyTabHistoryEntryDto
{
    public string? Id { get; set; }

    public string? Type { get; set; }

    public decimal? Amount { get; set; }

    public string? Note { get; set; }

    public string? Timestamp { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class LegacyTabDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? DisplayName { get; set; }

    public decimal? Balance { get; set; }

    public decimal? TabBalance { get; set; }

    public bool? Archived { get; set; }

    public bool? IsArchived { get; set; }

    public string? MemberId { get; set; }

    public bool? IsGuest { get; set; }

    public bool? Guest { get; set; }

    public string? TabType { get; set; }

    public List<LegacyTabHistoryEntryDto>? History { get; set; }

    public List<LegacyTabHistoryEntryDto>? TabHistory { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class LegacyMemberDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public decimal? Balance { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class LegacyBartenderDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Pin { get; set; }

    /// <summary>POSBarV2 hashed PIN (base64).</summary>
    public string? PinHash { get; set; }

    /// <summary>POSBarV2 PIN salt (base64).</summary>
    public string? PinSalt { get; set; }

    public string? Role { get; set; }

    public bool? Active { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class LegacyPitstopSaleDto
{
    public string? Id { get; set; }

    public string? Sku { get; set; }

    public string? ItemName { get; set; }

    public int? Quantity { get; set; }

    public decimal? Total { get; set; }

    public string? SoldAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>Loosely typed Square integration payload from V2.</summary>
public sealed class LegacySquareConfigDto
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
