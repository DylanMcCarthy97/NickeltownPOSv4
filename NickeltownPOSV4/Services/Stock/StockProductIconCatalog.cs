using System;
using System.Collections.Generic;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Built-in product thumbnails stored in Items.ImagePath as stock-icon:{id}.</summary>
public static class StockProductIconCatalog
{
    public const string StoragePrefix = "stock-icon:";

    private static readonly IReadOnlyList<StockProductIcon> Icons =
    [
        new("beer", "Beer", "\uD83C\uDF7A", "ms-appx:///Assets/BeerMugIcon.svg"),
        new("wine", "Wine", "\uD83C\uDF77"),
        new("spirits", "Spirits", "\uD83E\uDD43"),
        new("soft-drink", "Soft drink", "\uD83E\uDD64"),
        new("food", "Food", "\uD83C\uDF54"),
        new("snack", "Snack", "\uD83C\uDF5F"),
        new("merch", "Merch", "\uD83D\uDC55"),
        new("generic", "Drink", "\uD83E\uDD64"),
    ];

    public static IReadOnlyList<StockProductIcon> All => Icons;

    public static bool TryGetById(string? iconId, out StockProductIcon icon)
    {
        if (string.IsNullOrWhiteSpace(iconId))
        {
            icon = default!;
            return false;
        }

        var id = iconId.Trim();
        foreach (var entry in Icons)
        {
            if (string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                icon = entry;
                return true;
            }
        }

        icon = default!;
        return false;
    }

    public static bool TryGetFromStoragePath(string? imagePath, out StockProductIcon icon)
    {
        if (!TryParseStoragePath(imagePath, out var id))
        {
            icon = default!;
            return false;
        }

        return TryGetById(id, out icon);
    }

    public static bool IsStoragePath(string? imagePath) => TryParseStoragePath(imagePath, out _);

    public static bool TryParseStoragePath(string? imagePath, out string iconId)
    {
        iconId = string.Empty;
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        var trimmed = imagePath.Trim();
        if (!trimmed.StartsWith(StoragePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        iconId = trimmed[StoragePrefix.Length..].Trim();
        return iconId.Length > 0;
    }

    public static string ToStoragePath(string iconId) =>
        $"{StoragePrefix}{iconId.Trim()}";

    public static string GetDisplayEmoji(string? imagePath, string? catalogSubCategory) =>
        TryGetFromStoragePath(imagePath, out var icon)
            ? icon.Emoji
            : StockItemImageResolver.GetCategoryFallbackEmoji(catalogSubCategory);

    public sealed record StockProductIcon(string Id, string Label, string Emoji, string? PackagedSvgUri = null)
    {
        public bool HasPackagedSvg => !string.IsNullOrWhiteSpace(PackagedSvgUri);
    }
}
