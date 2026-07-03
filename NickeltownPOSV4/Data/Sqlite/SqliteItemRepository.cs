using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;
using NickeltownPOSV4.Services.Migration;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteItemRepository : IItemMigrationRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly IStockProductImageStorage _images;

    public SqliteItemRepository(SqliteConnectionFactory factory, IStockProductImageStorage images)
    {
        _factory = factory;
        _images = images;
    }

    public Task ImportItemsAsync(IReadOnlyList<LegacyItemDto> items, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var dto in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MigrationItemUpsert.Upsert(conn, tx, dto, "Item", _images);
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    internal static class MigrationItemUpsert
    {
        internal static void Upsert(
            SqliteConnection conn,
            SqliteTransaction tx,
            LegacyItemDto dto,
            string itemType,
            IStockProductImageStorage images)
        {
            var legacyId = string.IsNullOrWhiteSpace(dto.Id) ? LegacyStableId.ForItem(dto) : dto.Id!;
            var row = LegacyItemImportMapper.Map(dto, legacyId);
            var raw = JsonSerializer.Serialize(dto);
            var categoryId = SqliteCategoryRepository.ResolveCategoryIdForProduct(
                conn,
                tx,
                dto.CategoryId,
                row.LegacyCategoryLinkName);

            conn.Execute(
                """
                INSERT INTO Items (
                  LegacyId, LegacyKey, Name, Sku, CategoryId, ItemType, StockQty, TrackStock, ImagePath, RawJson,
                  IsActive, CostPrice, LowStockThreshold, UsesOpenPrice, ShowInPitstop, ShowInBar, OrderInMerchandise,
                  CatalogBucket, CatalogSubCategory, StockMode, NotGonnaOrderBack, IncludeInWeeklyStockReport, IsRunOutItem,
                  IsSharedItem, IsOnSpecial, SpecialType, SpecialValue, AlternateSkusJson, ItemDescription,
                  CreatedAt, UpdatedAt)
                VALUES (
                  @LegacyId, @LegacyKey, @Name, @Sku, @CategoryId, @ItemType, @StockQty, @TrackStock, @ImagePath, @RawJson,
                  @IsActive, @CostPrice, @LowStockThreshold, @UsesOpenPrice, @ShowInPitstop, @ShowInBar, @OrderInMerchandise,
                  @CatalogBucket, @CatalogSubCategory, @StockMode, @NotGonnaOrderBack, @IncludeInWeeklyStockReport, @IsRunOutItem,
                  @IsSharedItem, @IsOnSpecial, @SpecialType, @SpecialValue, @AlternateSkusJson, @ItemDescription,
                  datetime('now'), datetime('now'))
                ON CONFLICT(LegacyId) DO UPDATE SET
                  Name = excluded.Name,
                  Sku = excluded.Sku,
                  CategoryId = excluded.CategoryId,
                  StockQty = excluded.StockQty,
                  TrackStock = excluded.TrackStock,
                  ImagePath = excluded.ImagePath,
                  RawJson = excluded.RawJson,
                  IsActive = excluded.IsActive,
                  CostPrice = excluded.CostPrice,
                  LowStockThreshold = excluded.LowStockThreshold,
                  UsesOpenPrice = excluded.UsesOpenPrice,
                  ShowInPitstop = excluded.ShowInPitstop,
                  ShowInBar = excluded.ShowInBar,
                  OrderInMerchandise = excluded.OrderInMerchandise,
                  CatalogBucket = excluded.CatalogBucket,
                  CatalogSubCategory = excluded.CatalogSubCategory,
                  StockMode = excluded.StockMode,
                  NotGonnaOrderBack = excluded.NotGonnaOrderBack,
                  IncludeInWeeklyStockReport = excluded.IncludeInWeeklyStockReport,
                  IsRunOutItem = excluded.IsRunOutItem,
                  IsSharedItem = excluded.IsSharedItem,
                  IsOnSpecial = excluded.IsOnSpecial,
                  SpecialType = excluded.SpecialType,
                  SpecialValue = excluded.SpecialValue,
                  AlternateSkusJson = excluded.AlternateSkusJson,
                  ItemDescription = excluded.ItemDescription,
                  UpdatedAt = datetime('now')
                """,
                new
                {
                    row.LegacyId,
                    row.LegacyKey,
                    row.Name,
                    row.Sku,
                    CategoryId = categoryId,
                    ItemType = itemType,
                    row.StockQty,
                    row.TrackStock,
                    row.ImagePath,
                    RawJson = raw,
                    row.IsActive,
                    row.CostPrice,
                    row.LowStockThreshold,
                    row.UsesOpenPrice,
                    row.ShowInPitstop,
                    row.ShowInBar,
                    row.OrderInMerchandise,
                    row.CatalogBucket,
                    row.CatalogSubCategory,
                    row.StockMode,
                    row.NotGonnaOrderBack,
                    row.IncludeInWeeklyStockReport,
                    row.IsRunOutItem,
                    row.IsSharedItem,
                    row.IsOnSpecial,
                    row.SpecialType,
                    row.SpecialValue,
                    row.AlternateSkusJson,
                    row.ItemDescription,
                },
                tx);

            var itemPk = conn.QuerySingle<long>(
                "SELECT Id FROM Items WHERE LegacyId = @l",
                new { l = legacyId },
                tx);

            conn.Execute("DELETE FROM ItemPrices WHERE ItemId = @ItemId", new { ItemId = itemPk }, tx);
            InsertPrice(conn, tx, itemPk, "Bar", row.BarPrice);
            InsertPrice(conn, tx, itemPk, "Guest", row.GuestPrice);
            InsertPrice(conn, tx, itemPk, "Pitstop", row.PitstopPrice);
            InsertPrice(conn, tx, itemPk, "BarSpecial", row.BarSpecialPrice);
            InsertPrice(conn, tx, itemPk, "GuestSpecial", row.GuestSpecialPrice);
            InsertPrice(conn, tx, itemPk, "PitstopSpecial", row.PitstopSpecialPrice);

            var storedImage = images.ResolveImportImagePath(row.ImagePath, itemPk);
            if (!string.Equals(storedImage, row.ImagePath, StringComparison.Ordinal))
            {
                conn.Execute(
                    "UPDATE Items SET ImagePath = @img WHERE Id = @id",
                    new { img = storedImage, id = itemPk },
                    tx);
            }
        }

        private static void InsertPrice(SqliteConnection conn, SqliteTransaction tx, long itemPk, string kind, decimal? price)
        {
            if (price is not { } p)
            {
                return;
            }

            conn.Execute(
                """
                INSERT INTO ItemPrices (ItemId, Price, EffectiveFrom, CreatedAt, PriceKind)
                VALUES (@ItemId, @Price, datetime('now'), datetime('now'), @Kind)
                """,
                new { ItemId = itemPk, Price = p, Kind = kind },
                tx);
        }
    }
}
