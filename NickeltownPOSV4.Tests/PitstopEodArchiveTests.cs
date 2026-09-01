using System.Globalization;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopEodArchiveTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqlitePitstopEodBatchRepository _batches;
    private readonly SqliteStockEditingService _stock;

    public PitstopEodArchiveTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ntpos_eod_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
        new DatabaseInitializer(_factory).InitializeAsync().GetAwaiter().GetResult();
        _batches = new SqlitePitstopEodBatchRepository(_factory);
        _stock = new SqliteStockEditingService(_factory);
        using var conn = _factory.OpenConnection();
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        conn.Execute(
            "INSERT INTO Items (Name, StockQty, TrackStock, IsActive, CreatedAt, UpdatedAt, CatalogBucket, CatalogSubCategory) VALUES ('Shirt', 10, 1, 1, @now, @now, 'Pitstop', 'Merch')",
            new { now });
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
    public async Task Archive_AllowsOutsideOnlyDayWithZeroPosSales()
    {
        var start = DateTimeOffset.Now.Date;
        var end = start.AddDays(1);
        var first = await _batches.ArchiveActivePitstopSalesAsync(Request(start, end, "Merch day"));
        Assert.True(first.Ok, first.ErrorMessage);
        Assert.Equal(0, first.SalesArchived);
        Assert.NotNull(first.BatchId);

        var existing = await _batches.GetLatestBatchIdForPeriodAsync(start, end);
        Assert.Equal(first.BatchId, existing);
    }

    [Fact]
    public async Task StockDeduction_IsIdempotentForSameBatchAndItem()
    {
        var start = DateTimeOffset.Now.Date;
        var end = start.AddDays(1);
        var archive = await _batches.ArchiveActivePitstopSalesAsync(Request(start, end, "Stock day"));
        Assert.True(archive.Ok, archive.ErrorMessage);
        var batchId = archive.BatchId!.Value;

        Assert.True(await _stock.ApplyPitstopEodDeductionAsync(batchId, 1, 2));
        Assert.True(await _stock.ApplyPitstopEodDeductionAsync(batchId, 1, 2));

        using var conn = _factory.OpenConnection();
        Assert.Equal(8, conn.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = 1"));
        Assert.Equal(1L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM StockMovements WHERE ItemId = 1 AND Reason = 'PitstopEod'"));
    }

    private static PitstopEodArchiveRequest Request(DateTimeOffset start, DateTimeOffset end, string name) =>
        new()
        {
            OperatorName = "Dylan",
            EventName = name,
            PeriodStartLocal = start,
            PeriodEndLocal = end,
            TotalSales = 160m,
            ReportData = new PitstopReportData
            {
                EventName = name,
                GrossSales = 160m,
                StaffName = "Dylan",
            },
        };
}
