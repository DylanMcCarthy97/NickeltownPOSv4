using System.Diagnostics;
using System.Globalization;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopCatalogQueryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqlitePitstopCatalogQuery _query;

    public PitstopCatalogQueryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ntpos_pitcat_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
        new DatabaseInitializer(_factory).InitializeAsync().GetAwaiter().GetResult();
        _query = new SqlitePitstopCatalogQuery(_factory);
        Seed();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // ignore temp cleanup
        }
    }

    [Fact]
    public async Task GetPitstopProductsAsync_ReturnsSellablePitstopItems()
    {
        var products = await _query.GetPitstopProductsAsync(null);
        var water = Assert.Single(products, p => p.Name == "Water");
        Assert.Equal(2.00d, water.PitstopPrice);
        Assert.Contains("Drinks", await _query.GetPitstopCategoryNamesAsync());
        var bySku = await _query.FindBySkuAsync("WAT");
        Assert.NotNull(bySku);
        Assert.Equal(water.ItemId, bySku!.ItemId);
        Assert.Null(await _query.FindBySkuAsync(" "));
    }

    [Fact]
    public async Task GetPitstopProductsAsync_CompletesWithoutThrowing()
    {
        var sw = Stopwatch.StartNew();
        var products = await _query.GetPitstopProductsAsync(null);
        sw.Stop();

        Assert.NotEmpty(products);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Pitstop catalog query took {sw.ElapsedMilliseconds}ms");
    }

    private void Seed()
    {
        using var conn = _factory.OpenConnection();
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        conn.Execute(
            """
            INSERT INTO Items (Name, Sku, StockQty, TrackStock, IsActive, CreatedAt, UpdatedAt, CatalogBucket, CatalogSubCategory, ShowInBar, ShowInPitstop)
            VALUES ('Water', 'WAT', 30, 1, 1, @now, @now, 'Pitstop', 'Drinks', 0, 1)
            """,
            new { now });
        conn.Execute(
            """
            INSERT INTO ItemPrices (ItemId, Price, EffectiveFrom, CreatedAt, PriceKind)
            VALUES (1, 2.00, @now, @now, 'Pitstop')
            """,
            new { now });
    }
}
