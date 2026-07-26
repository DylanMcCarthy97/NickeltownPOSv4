using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.CustomAmount;

public interface ICustomAmountBootstrapper
{
    Task EnsureAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Ensures a Shared open-price "Custom Amount" item exists so both Bar and Pitstop catalogs
/// show a tile that prompts for an amount at sale time.
/// </summary>
public sealed class CustomAmountBootstrapper : ICustomAmountBootstrapper
{
    private readonly SqliteConnectionFactory _factory;

    public CustomAmountBootstrapper(SqliteConnectionFactory factory) => _factory = factory;

    public Task EnsureAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => EnsureExistsCore(cancellationToken), cancellationToken);

    private void EnsureExistsCore(CancellationToken cancellationToken)
    {
        using var conn = _factory.OpenConnection();
        var existing = conn.QuerySingleOrDefault<long?>(
            new CommandDefinition(
                """
                SELECT Id FROM Items
                WHERE COALESCE(IsActive, 1) != 0
                  AND (
                    lower(trim(COALESCE(ItemType, ''))) = lower(@type)
                    OR lower(trim(Name)) = lower(@name)
                    OR lower(trim(COALESCE(LegacyKey, ''))) = lower(@legacy)
                  )
                ORDER BY Id
                LIMIT 1
                """,
                new
                {
                    type = CustomAmountCatalog.ItemType,
                    name = CustomAmountCatalog.ItemName,
                    legacy = CustomAmountCatalog.LegacyKey,
                },
                cancellationToken: cancellationToken));

        if (existing is > 0)
        {
            return;
        }

        var legacy = CustomAmountCatalog.LegacyKey;
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO Items (
                  LegacyId, LegacyKey, Name, Sku, CategoryId, ItemType, StockQty, TrackStock,
                  ImagePath, RawJson, IsActive, CreatedAt, UpdatedAt,
                  CatalogBucket, CatalogSubCategory, StockMode,
                  ShowInBar, ShowInPitstop, OrderInMerchandise, UsesOpenPrice, ItemDescription)
                VALUES (
                  @LegacyId, @LegacyKey, @Name, NULL, NULL, @ItemType, 0, 0,
                  NULL, '{}', 1, datetime('now'), datetime('now'),
                  @Bucket, @SubCategory, @StockMode,
                  1, 1, 0, 1, NULL)
                """,
                new
                {
                    LegacyId = legacy,
                    LegacyKey = legacy,
                    Name = CustomAmountCatalog.ItemName,
                    ItemType = CustomAmountCatalog.ItemType,
                    Bucket = StockCatalogTaxonomy.BucketShared,
                    SubCategory = "Drinks",
                    StockMode = StockCatalogTaxonomy.StockModeNotTracked,
                },
                cancellationToken: cancellationToken));
    }
}
