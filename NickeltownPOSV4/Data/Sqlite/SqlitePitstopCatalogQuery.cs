using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqlitePitstopCatalogQuery : IPitstopCatalogQuery
{
    private readonly SqliteConnectionFactory _factory;

    public SqlitePitstopCatalogQuery(SqliteConnectionFactory factory) => _factory = factory;

    private const string PitstopBucketExpr =
        "trim(COALESCE(NULLIF(TRIM(i.CatalogBucket), ''), 'Bar'))";

    private const string PitstopSubCategoryExpr =
        "trim(COALESCE(NULLIF(TRIM(i.CatalogSubCategory), ''), 'Drinks'))";

    /// <summary>Pitstop POS sells Pitstop-bucket and Shared-bucket items (not Bar-only catalog lines).</summary>
    private const string PitstopSellableBucketFilter =
        $" AND lower(trim({PitstopBucketExpr})) IN ('pitstop', 'shared') ";

    private const string LatestBarPriceSubquery =
        """
        (
          SELECT ip2.Price
          FROM ItemPrices ip2
          WHERE ip2.ItemId = i.Id AND COALESCE(ip2.PriceKind, 'Bar') = 'Bar'
          ORDER BY datetime(ip2.EffectiveFrom) DESC, ip2.Id DESC
          LIMIT 1
        )
        """;

    private const string LatestPitstopPriceSubquery =
        """
        (
          SELECT ipp.Price
          FROM ItemPrices ipp
          WHERE ipp.ItemId = i.Id AND lower(trim(COALESCE(ipp.PriceKind, ''))) = 'pitstop'
          ORDER BY datetime(ipp.EffectiveFrom) DESC, ipp.Id DESC
          LIMIT 1
        )
        """;

    /// <summary>Explicit Pitstop price, or bar price when bucket is Shared and Pitstop is unset.</summary>
    private static readonly string EffectivePitstopPriceExpr =
        $"""
        CAST(COALESCE(
          NULLIF({LatestPitstopPriceSubquery}, 0),
          CASE
            WHEN lower(trim({PitstopBucketExpr})) = 'shared'
            THEN NULLIF({LatestBarPriceSubquery}, 0)
            ELSE NULL
          END,
          0
        ) AS REAL)
        """;

    private static readonly string HasSellablePitstopPriceFilter =
        $"""
         AND (
           EXISTS (
             SELECT 1
             FROM ItemPrices ipp
             WHERE ipp.ItemId = i.Id AND lower(trim(COALESCE(ipp.PriceKind, ''))) = 'pitstop'
               AND ipp.Price > 0
           )
           OR (
             lower(trim({PitstopBucketExpr})) = 'shared'
             AND EXISTS (
               SELECT 1
               FROM ItemPrices ipb
               WHERE ipb.ItemId = i.Id AND COALESCE(ipb.PriceKind, 'Bar') = 'Bar'
                 AND ipb.Price > 0
             )
           )
         )
        """;

    public Task<IReadOnlyList<string>> GetPitstopCategoryNamesAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var sql = $"""
            SELECT DISTINCT {PitstopSubCategoryExpr} AS Cat
            FROM Items i
            WHERE COALESCE(i.IsActive, 1) != 0
              AND COALESCE(i.ShowInPitstop, 0) != 0
              {PitstopSellableBucketFilter}
              {HasSellablePitstopPriceFilter}
            ORDER BY Cat COLLATE NOCASE
            """;
        var rows = conn.Query<string>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<string>>(rows.AsList());
    }

    public Task<IReadOnlyList<PitstopCatalogProductRow>> GetPitstopProductsAsync(string? categoryName, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var sql = $"""
            SELECT
              i.Id AS ItemId,
              i.Name,
              {PitstopSubCategoryExpr} AS CategoryName,
              {PitstopBucketExpr} AS CatalogBucket,
              {EffectivePitstopPriceExpr} AS PitstopPrice,
              CAST(COALESCE((
                SELECT ips.Price
                FROM ItemPrices ips
                WHERE ips.ItemId = i.Id AND lower(trim(COALESCE(ips.PriceKind, ''))) = 'pitstopspecial'
                ORDER BY datetime(ips.EffectiveFrom) DESC, ips.Id DESC
                LIMIT 1
              ), 0) AS REAL) AS PitstopSpecialPrice,
              COALESCE(i.IsOnSpecial, 0) AS IsOnSpecial,
              i.StockQty,
              COALESCE(i.TrackStock, 1) AS TrackStock,
              i.ItemType,
              i.ImagePath,
              i.Sku AS Sku,
              COALESCE(i.UsesOpenPrice, 0) AS UsesOpenPrice,
              COALESCE(i.OrderInMerchandise, 0) AS OrderInMerchandise,
              i.LowStockThreshold AS LowStockThreshold
            FROM Items i
            WHERE COALESCE(i.IsActive, 1) != 0
              AND COALESCE(i.ShowInPitstop, 0) != 0
              {PitstopSellableBucketFilter}
              {HasSellablePitstopPriceFilter}
              AND (@CategoryName IS NULL OR {PitstopSubCategoryExpr} = @CategoryName)
            ORDER BY i.Name COLLATE NOCASE
            """;

        var rows = conn.Query<PitstopCatalogProductRow>(
            new CommandDefinition(sql, new { CategoryName = categoryName }, cancellationToken: cancellationToken));
        var list = rows.AsList();
        foreach (var r in list)
        {
            SplitCategory(r);
        }

        return Task.FromResult<IReadOnlyList<PitstopCatalogProductRow>>(list);
    }

    public Task<PitstopCatalogProductRow?> FindBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        var needle = (sku ?? string.Empty).Trim();
        if (needle.Length == 0)
        {
            return Task.FromResult<PitstopCatalogProductRow?>(null);
        }

        using var conn = _factory.OpenConnection();
        var sql = $"""
            SELECT
              i.Id AS ItemId,
              i.Name,
              {PitstopSubCategoryExpr} AS CategoryName,
              {PitstopBucketExpr} AS CatalogBucket,
              {EffectivePitstopPriceExpr} AS PitstopPrice,
              CAST(COALESCE((
                SELECT ips.Price
                FROM ItemPrices ips
                WHERE ips.ItemId = i.Id AND lower(trim(COALESCE(ips.PriceKind, ''))) = 'pitstopspecial'
                ORDER BY datetime(ips.EffectiveFrom) DESC, ips.Id DESC
                LIMIT 1
              ), 0) AS REAL) AS PitstopSpecialPrice,
              COALESCE(i.IsOnSpecial, 0) AS IsOnSpecial,
              i.StockQty,
              COALESCE(i.TrackStock, 1) AS TrackStock,
              i.ItemType,
              i.ImagePath,
              i.Sku AS Sku,
              COALESCE(i.UsesOpenPrice, 0) AS UsesOpenPrice,
              COALESCE(i.OrderInMerchandise, 0) AS OrderInMerchandise,
              i.LowStockThreshold AS LowStockThreshold
            FROM Items i
            WHERE COALESCE(i.IsActive, 1) != 0
              AND COALESCE(i.ShowInPitstop, 0) != 0
              {PitstopSellableBucketFilter}
              AND lower(trim(COALESCE(i.Sku, ''))) = lower(trim(@Sku))
            LIMIT 1
            """;

        var row = conn.QuerySingleOrDefault<PitstopCatalogProductRow>(
            new CommandDefinition(sql, new { Sku = needle }, cancellationToken: cancellationToken));
        if (row is not null)
        {
            SplitCategory(row);
        }

        return Task.FromResult(row);
    }

    private static void SplitCategory(PitstopCatalogProductRow r)
    {
        var sub = (r.CategoryName ?? string.Empty).Trim();
        r.SubCategoryLabel = sub;
        var bucket = (r.CatalogBucket ?? string.Empty).Trim();
        r.CategoryName = bucket.Length == 0 ? StockCatalogTaxonomy.BucketPitstop : StockCatalogTaxonomy.NormalizeBucket(bucket);
    }
}
