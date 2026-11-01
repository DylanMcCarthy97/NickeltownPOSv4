using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services;

public sealed class BarCatalogCache : IBarCatalogCache
{
    private readonly SqliteBarCatalogQuery _query;
    private readonly object _gate = new();
    private BarCatalogSnapshot? _snapshot;
    private Task<BarCatalogSnapshot>? _loading;

    public BarCatalogCache(SqliteBarCatalogQuery query) => _query = query;

    public void Invalidate()
    {
        lock (_gate)
        {
            _snapshot = null;
            _loading = null;
        }
    }

    public Task<BarCatalogSnapshot> GetOrLoadAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_snapshot is not null)
            {
                return Task.FromResult(_snapshot);
            }

            _loading ??= LoadCoreAsync(cancellationToken);
            return _loading;
        }
    }

    private async Task<BarCatalogSnapshot> LoadCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categoriesTask = _query.GetBarCategoryNamesAsync(cancellationToken);
            var productsTask = _query.GetBarProductsAsync(null, cancellationToken);
            await Task.WhenAll(categoriesTask, productsTask).ConfigureAwait(false);
            var categories = await categoriesTask.ConfigureAwait(false);
            var products = await productsTask.ConfigureAwait(false);
            var snapshot = new BarCatalogSnapshot
            {
                CategoryNames = categories,
                Products = products,
            };

            lock (_gate)
            {
                _snapshot = snapshot;
            }

            return snapshot;
        }
        finally
        {
            lock (_gate)
            {
                _loading = null;
            }
        }
    }
}