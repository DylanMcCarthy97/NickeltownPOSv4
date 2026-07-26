using System;

namespace NickeltownPOSV4.Services.CustomAmount;

/// <summary>Identifies the seeded Custom Amount SKU used on Bar and Pitstop for open-price sales.</summary>
public static class CustomAmountCatalog
{
    public const string ItemName = "Custom Amount";

    public const string ItemType = "CustomAmount";

    public const string LegacyKey = "v4_custom_amount";

    public static bool IsCustomAmountItem(string? name, string? itemType) =>
        string.Equals(itemType, ItemType, StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(name)
            && name.Equals(ItemName, StringComparison.OrdinalIgnoreCase));
}
