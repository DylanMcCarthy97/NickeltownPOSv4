using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>In-memory index of ClubPOS products for mapping Square catalog line items.</summary>
public sealed class SquareClubPosProductCatalogIndex
{
    private readonly Dictionary<string, CatalogEntry> _byNormalizedName = new(StringComparer.OrdinalIgnoreCase);

    public sealed class CatalogEntry
    {
        public long ItemId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string SubCategory { get; init; } = string.Empty;
    }

    public static async Task<SquareClubPosProductCatalogIndex> BuildAsync(
        IPitstopCatalogQuery pitstopCatalog,
        IItemCatalogQuery barCatalog,
        CancellationToken cancellationToken = default)
    {
        var index = new SquareClubPosProductCatalogIndex();
        var pitstopProducts = await pitstopCatalog.GetPitstopProductsAsync(null, cancellationToken).ConfigureAwait(false);
        foreach (var product in pitstopProducts)
        {
            index.Add(product.ItemId, product.Name, product.SubCategoryLabel);
        }

        var barProducts = await barCatalog.GetBarProductsAsync(null, cancellationToken).ConfigureAwait(false);
        foreach (var product in barProducts)
        {
            index.Add(product.ItemId, product.Name, product.CatalogSubCategory);
        }

        return index;
    }

    public CatalogEntry? TryMatch(string? itemName, string? variationName = null)
    {
        var entry = TryMatchName(itemName);
        if (entry is not null)
        {
            return entry;
        }

        return TryMatchName(variationName);
    }

    private CatalogEntry? TryMatchName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var normalized = NormalizeName(name);
        if (_byNormalizedName.TryGetValue(normalized, out var exact))
        {
            return exact;
        }

        foreach (var pair in _byNormalizedName)
        {
            if (normalized.Contains(pair.Key, StringComparison.OrdinalIgnoreCase)
                || pair.Key.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private void Add(long itemId, string name, string subCategory)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var normalized = NormalizeName(name);
        if (!_byNormalizedName.ContainsKey(normalized))
        {
            _byNormalizedName[normalized] = new CatalogEntry
            {
                ItemId = itemId,
                Name = name.Trim(),
                SubCategory = subCategory?.Trim() ?? string.Empty,
            };
        }
    }

    private static string NormalizeName(string name) =>
        name.Trim().Replace("  ", " ", StringComparison.Ordinal);
}
