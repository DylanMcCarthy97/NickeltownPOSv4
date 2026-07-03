using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;
using NickeltownPOSV4.Services.Migration;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteDrinkRepository : IDrinkMigrationRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly IStockProductImageStorage _images;

    public SqliteDrinkRepository(SqliteConnectionFactory factory, IStockProductImageStorage images)
    {
        _factory = factory;
        _images = images;
    }

    public Task ImportDrinksAsync(IReadOnlyList<LegacyDrinkDto> drinks, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var drink in drinks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemDto = new LegacyItemDto
            {
                Id = drink.Id,
                Name = drink.Name,
                Category = drink.Category,
                SubCategory = drink.Category,
                CategoryId = drink.CategoryId,
                Sku = drink.Sku,
                Barcode = drink.Barcode,
                Price = drink.Price,
                Stock = drink.Stock,
                Quantity = drink.Quantity,
                OnHand = drink.OnHand,
                Amount = drink.Amount,
                TrackStock = drink.TrackStock,
                TrackInventory = drink.TrackInventory,
                ImagePath = drink.ImagePath,
                ExtensionData = drink.ExtensionData,
            };
            SqliteItemRepository.MigrationItemUpsert.Upsert(conn, tx, itemDto, "Drink", _images);
        }

        tx.Commit();
        return Task.CompletedTask;
    }
}
