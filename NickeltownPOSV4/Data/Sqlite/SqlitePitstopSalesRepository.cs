using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqlitePitstopSalesRepository : IPitstopSalesMigrationRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqlitePitstopSalesRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task ImportSalesAsync(IReadOnlyList<LegacyPitstopSaleDto> sales, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var dto in sales)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var legacyId = string.IsNullOrWhiteSpace(dto.Id) ? LegacyStableId.ForPitstopSale(dto) : dto.Id!;
            var raw = JsonSerializer.Serialize(dto);
            conn.Execute(
                """
                INSERT INTO PitstopSales (LegacyId, SoldAt, Total, RawJson, CreatedAt)
                VALUES (@LegacyId, @SoldAt, @Total, @RawJson, datetime('now'))
                ON CONFLICT(LegacyId) DO UPDATE SET
                  SoldAt = excluded.SoldAt,
                  Total = excluded.Total,
                  RawJson = excluded.RawJson
                """,
                new
                {
                    LegacyId = legacyId,
                    SoldAt = dto.SoldAt,
                    Total = dto.Total ?? 0m,
                    RawJson = raw,
                },
                tx);

            var salePk = conn.QuerySingle<long>(
                "SELECT Id FROM PitstopSales WHERE LegacyId = @l",
                new { l = legacyId },
                tx);

            conn.Execute(
                "DELETE FROM PitstopSaleItems WHERE PitstopSaleId = @id",
                new { id = salePk },
                tx);

            conn.Execute(
                """
                INSERT INTO PitstopSaleItems (PitstopSaleId, LegacyLineId, Sku, ItemName, Quantity, LineTotal, RawJson, CreatedAt)
                VALUES (@PitstopSaleId, @LegacyLineId, @Sku, @ItemName, @Quantity, @LineTotal, @RawJson, datetime('now'))
                """,
                new
                {
                    PitstopSaleId = salePk,
                    LegacyLineId = legacyId + ":line",
                    Sku = dto.Sku,
                    ItemName = dto.ItemName,
                    Quantity = dto.Quantity ?? 0,
                    LineTotal = dto.Total,
                    RawJson = raw,
                },
                tx);
        }

        tx.Commit();
        return Task.CompletedTask;
    }
}
