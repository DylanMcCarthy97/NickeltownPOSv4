using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqlitePitstopHeldSaleRepository : IPitstopHeldSaleRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqlitePitstopHeldSaleRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<int> GetHeldSaleCountAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var n = conn.ExecuteScalar<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM PitstopHeldSales",
                cancellationToken: cancellationToken));
        return Task.FromResult(n);
    }

    public Task<IReadOnlyList<PitstopHeldSaleSummaryRow>> ListHeldSalesAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<HeldSummaryDbRow>(
            new CommandDefinition(
                """
                SELECT Id, HeldAt, LineCount, TotalAmount, StaffDisplayName
                FROM PitstopHeldSales
                ORDER BY HeldAt DESC, Id DESC
                """,
                cancellationToken: cancellationToken));

        IReadOnlyList<PitstopHeldSaleSummaryRow> list = rows
            .Select(r => new PitstopHeldSaleSummaryRow
            {
                Id = r.Id,
                HeldAt = ParseStamp(r.HeldAt),
                LineCount = r.LineCount,
                TotalAmount = r.TotalAmount,
                StaffDisplayName = r.StaffDisplayName,
            })
            .ToList();

        return Task.FromResult(list);
    }

    public Task<long> SaveHeldSaleAsync(
        IReadOnlyList<PitstopHeldSaleLineWrite> lines,
        long? staffId,
        string? staffDisplayName,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            throw new ArgumentException("At least one line is required.", nameof(lines));
        }

        var merged = MergeLines(lines);
        var total = decimal.Round(merged.Sum(l => l.UnitPrice * l.Quantity), 2, MidpointRounding.AwayFromZero);
        var heldAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var createdAt = heldAt;

        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        var id = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                INSERT INTO PitstopHeldSales (HeldAt, StaffId, StaffDisplayName, LineCount, TotalAmount, CreatedAt)
                VALUES (@heldAt, @staffId, @staffName, @lineCount, @total, @createdAt);
                SELECT last_insert_rowid();
                """,
                new
                {
                    heldAt,
                    staffId,
                    staffName = string.IsNullOrWhiteSpace(staffDisplayName) ? null : staffDisplayName.Trim(),
                    lineCount = merged.Count,
                    total,
                    createdAt,
                },
                tx,
                cancellationToken: cancellationToken));

        var sort = 0;
        foreach (var line in merged)
        {
            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO PitstopHeldSaleLines
                      (HeldSaleId, ItemId, ItemName, Sku, CategoryName, SubCategory, UnitPrice, Quantity, SortOrder)
                    VALUES
                      (@heldSaleId, @itemId, @name, @sku, @cat, @sub, @price, @qty, @sort);
                    """,
                    new
                    {
                        heldSaleId = id,
                        itemId = line.ItemId,
                        name = line.ItemName,
                        sku = line.Sku,
                        cat = line.CategoryName,
                        sub = line.SubCategory,
                        price = line.UnitPrice,
                        qty = line.Quantity,
                        sort = sort++,
                    },
                    tx,
                    cancellationToken: cancellationToken));
        }

        tx.Commit();
        return Task.FromResult(id);
    }

    public Task<PitstopHeldSaleDetail?> GetHeldSaleAsync(long heldSaleId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var header = conn.ExecuteScalar<long?>(
            new CommandDefinition(
                "SELECT Id FROM PitstopHeldSales WHERE Id = @id",
                new { id = heldSaleId },
                cancellationToken: cancellationToken));

        if (header is null)
        {
            return Task.FromResult<PitstopHeldSaleDetail?>(null);
        }

        var lineRows = conn.Query<HeldLineDbRow>(
            new CommandDefinition(
                """
                SELECT ItemId, ItemName, Sku, CategoryName, SubCategory, UnitPrice, Quantity
                FROM PitstopHeldSaleLines
                WHERE HeldSaleId = @id
                ORDER BY SortOrder, Id
                """,
                new { id = heldSaleId },
                cancellationToken: cancellationToken));

        var lines = lineRows
            .Select(r => new PitstopHeldSaleLineRow
            {
                ItemId = r.ItemId,
                ItemName = r.ItemName ?? string.Empty,
                Sku = r.Sku,
                CategoryName = r.CategoryName,
                SubCategory = r.SubCategory,
                UnitPrice = r.UnitPrice,
                Quantity = r.Quantity,
            })
            .ToList();

        return Task.FromResult<PitstopHeldSaleDetail?>(new PitstopHeldSaleDetail
        {
            Id = heldSaleId,
            Lines = lines,
        });
    }

    public Task DeleteHeldSaleAsync(long heldSaleId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(
            new CommandDefinition(
                "DELETE FROM PitstopHeldSales WHERE Id = @id",
                new { id = heldSaleId },
                cancellationToken: cancellationToken));
        return Task.CompletedTask;
    }

    private static List<PitstopHeldSaleLineWrite> MergeLines(IReadOnlyList<PitstopHeldSaleLineWrite> lines)
    {
        var map = new Dictionary<long, PitstopHeldSaleLineWrite>();
        foreach (var line in lines)
        {
            if (map.TryGetValue(line.ItemId, out var existing))
            {
                map[line.ItemId] = new PitstopHeldSaleLineWrite
                {
                    ItemId = line.ItemId,
                    ItemName = existing.ItemName,
                    Sku = existing.Sku ?? line.Sku,
                    CategoryName = existing.CategoryName ?? line.CategoryName,
                    SubCategory = existing.SubCategory ?? line.SubCategory,
                    UnitPrice = existing.UnitPrice,
                    Quantity = existing.Quantity + line.Quantity,
                };
            }
            else
            {
                map[line.ItemId] = line;
            }
        }

        return map.Values.ToList();
    }

    private static DateTimeOffset ParseStamp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return DateTimeOffset.UtcNow;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto;
        }

        return DateTimeOffset.UtcNow;
    }

    private sealed class HeldSummaryDbRow
    {
        public long Id { get; set; }

        public string? HeldAt { get; set; }

        public int LineCount { get; set; }

        public decimal TotalAmount { get; set; }

        public string? StaffDisplayName { get; set; }
    }

    private sealed class HeldLineDbRow
    {
        public long ItemId { get; set; }

        public string? ItemName { get; set; }

        public string? Sku { get; set; }

        public string? CategoryName { get; set; }

        public string? SubCategory { get; set; }

        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }
    }
}
