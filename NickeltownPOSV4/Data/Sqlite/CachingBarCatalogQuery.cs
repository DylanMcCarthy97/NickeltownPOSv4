using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Filters the in-memory <see cref="IBarCatalogCache"/> snapshot.</summary>
public sealed class CachingBarCatalogQuery : IItemCatalogQuery
{
    private readonly IBarCatalogCache _cache;

    public CachingBarCatalogQuery(IBarCatalogCache cache) => _cache = cache;

    public async Task<IReadOnlyList<string>> GetBarCategoryNamesAsync(CancellationToken cancellationToken = default)
    {
        var snap = await _cache.GetOrLoadAsync(cancellationToken).ConfigureAwait(false);
        return snap.CategoryNames;
    }

    public async Task<IReadOnlyList<BarCatalogProductRow>> GetBarProductsAsync(
        string? categoryName,
        CancellationToken cancellationToken = default)
    {
        var snap = await _cache.GetOrLoadAsync(cancellationToken).ConfigureAwait(false);
        if (categoryName is null)
        {
            return snap.Products;
        }

        return snap.Products
            .Where(p => string.Equals(p.CategoryName, categoryName, System.StringComparison.Ordinal))
            .ToList();
    }
}