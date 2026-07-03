using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteBarCatalogQuery : IItemCatalogQuery
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteBarCatalogQuery(SqliteConnectionFactory factory) => _factory = factory;

    private const string CategoryExpr =
        """
        trim(COALESCE(NULLIF(TRIM(i.CatalogBucket), ''), 'Bar'))
          || ' / '
          || trim(COALESCE(NULLIF(TRIM(i.CatalogSubCategory), ''), 'Drinks'))
        """;

    public Task<IReadOnlyList<string>> GetBarCategoryNamesAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var sql = $"""
            SELECT DISTINCT {CategoryExpr} AS Cat
            FROM Items i
            WHERE COALESCE(i.IsActive, 1) != 0
              AND COALESCE(i.ShowInBar, 1) != 0
              AND lower(trim(COALESCE(NULLIF(TRIM(i.CatalogBucket), ''), 'Bar'))) IN ('bar', 'shared')
              AND (
                EXISTS (
                  SELECT 1
                  FROM ItemPrices ip
                  WHERE ip.ItemId = i.Id AND COALESCE(ip.PriceKind, 'Bar') = 'Bar'
                )
                OR COALESCE(i.UsesOpenPrice, 0) != 0
                OR EXISTS (
                  SELECT 1
                  FROM ItemPrices ipp
                  WHERE ipp.ItemId = i.Id
                    AND lower(trim(COALESCE(ipp.PriceKind, ''))) = 'pitstop'
                    AND ipp.Price > 0
                )
              )
            ORDER BY Cat
            """;
        var rows = conn.Query<string>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<string>>(rows.AsList());
    }

    public Task<IReadOnlyList<BarCatalogProductRow>> GetBarProductsAsync(string? categoryName, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var sql = $"""
            SELECT
              i.Id AS ItemId,
              i.Name,
              {CategoryExpr} AS CategoryName,
              COALESCE(NULLIF(TRIM(i.CatalogBucket), ''), 'Bar') AS CatalogBucket,
              COALESCE(NULLIF(TRIM(i.CatalogSubCategory), ''), 'Drinks') AS CatalogSubCategory,
              CAST(COALESCE((
                SELECT ip2.Price
                FROM ItemPrices ip2
                WHERE ip2.ItemId = i.Id AND COALESCE(ip2.PriceKind, 'Bar') = 'Bar'
                ORDER BY datetime(ip2.EffectiveFrom) DESC, ip2.Id DESC
                LIMIT 1
              ), 0) AS REAL) AS BarPrice,
              CAST(COALESCE((
                SELECT ipg.Price
                FROM ItemPrices ipg
                WHERE ipg.ItemId = i.Id AND lower(trim(COALESCE(ipg.PriceKind, ''))) = 'guest'
                ORDER BY datetime(ipg.EffectiveFrom) DESC, ipg.Id DESC
                LIMIT 1
              ), 0) AS REAL) AS GuestPrice,
              CAST(COALESCE((
                SELECT ipp.Price
                FROM ItemPrices ipp
                WHERE ipp.ItemId = i.Id AND lower(trim(COALESCE(ipp.PriceKind, ''))) = 'pitstop'
                ORDER BY datetime(ipp.EffectiveFrom) DESC, ipp.Id DESC
                LIMIT 1
              ), 0) AS REAL) AS PitstopPrice,
              CAST(COALESCE((
                SELECT ips.Price
                FROM ItemPrices ips
                WHERE ips.ItemId = i.Id AND lower(trim(COALESCE(ips.PriceKind, ''))) = 'barspecial'
                ORDER BY datetime(ips.EffectiveFrom) DESC, ips.Id DESC
                LIMIT 1
              ), 0) AS REAL) AS BarSpecialPrice,
              CAST(COALESCE((
                SELECT ipgs.Price
                FROM ItemPrices ipgs
                WHERE ipgs.ItemId = i.Id AND lower(trim(COALESCE(ipgs.PriceKind, ''))) = 'guestspecial'
                ORDER BY datetime(ipgs.EffectiveFrom) DESC, ipgs.Id DESC
                LIMIT 1
              ), 0) AS REAL) AS GuestSpecialPrice,
              COALESCE(i.IsOnSpecial, 0) AS IsOnSpecial,
              i.StockQty,
              COALESCE(i.TrackStock, 1) AS TrackStock,
              i.ItemType,
              i.ImagePath,
              i.Sku AS Sku,
              i.AlternateSkusJson AS AlternateSkusJson,
              COALESCE(i.UsesOpenPrice, 0) AS UsesOpenPrice,
              COALESCE(i.OrderInMerchandise, 0) AS OrderInMerchandise
            FROM Items i
            WHERE COALESCE(i.IsActive, 1) != 0
              AND COALESCE(i.ShowInBar, 1) != 0
              AND lower(trim(COALESCE(NULLIF(TRIM(i.CatalogBucket), ''), 'Bar'))) IN ('bar', 'shared')
              AND lower(trim(COALESCE(i.ItemType, ''))) != 'shotmixer'
              AND lower(trim(i.Name)) NOT LIKE '%shot + mixer%'
              AND (
                EXISTS (
                  SELECT 1
                  FROM ItemPrices ipb
                  WHERE ipb.ItemId = i.Id AND COALESCE(ipb.PriceKind, 'Bar') = 'Bar'
                )
                OR COALESCE(i.UsesOpenPrice, 0) != 0
                OR EXISTS (
                  SELECT 1
                  FROM ItemPrices ipp
                  WHERE ipp.ItemId = i.Id
                    AND lower(trim(COALESCE(ipp.PriceKind, ''))) = 'pitstop'
                    AND ipp.Price > 0
                )
              )
              AND (@CategoryName IS NULL OR {CategoryExpr} = @CategoryName)
            ORDER BY i.Name COLLATE NOCASE
            """;

        var rows = conn.Query<BarCatalogProductRow>(
            new CommandDefinition(sql, new { CategoryName = categoryName }, cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<BarCatalogProductRow>>(rows.AsList());
    }
}
