using System.Globalization;
using System.Text.Json;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Payments;
using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PaymentPipelineTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteTabFundsService _funds;
    private readonly SqliteTabDrinkSalesService _drinks;
    private readonly SqlitePitstopRetailSaleRepository _pitstop;
    private readonly ISquarePaymentAttemptRepository _attempts;
    private readonly SqliteSquareRecoveryRepository _recovery;
    private readonly PitstopPaymentRecoveryService _paymentRecovery;

    public PaymentPipelineTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ntpos_test_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
        new DatabaseInitializer(_factory).InitializeAsync().GetAwaiter().GetResult();
        _attempts = new SqliteSquarePaymentAttemptRepository(_factory);
        _recovery = new SqliteSquareRecoveryRepository(_factory);
        _funds = new SqliteTabFundsService(_factory, _attempts);
        _drinks = new SqliteTabDrinkSalesService(_factory);
        _pitstop = new SqlitePitstopRetailSaleRepository(_factory, _attempts);
        _paymentRecovery = new PitstopPaymentRecoveryService(
            _recovery,
            _pitstop,
            _attempts,
            new NoOpAuditLogService());
        Seed();
    }

    public void Dispose() { try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { } }

    [Fact]
    public async Task SquareTabTopUp_Idempotent()
    {
        var meta = new SquarePaymentCommitMetadata { IdempotencyKey = "k1", SquarePaymentId = "sq1", BaseAmount = 50m, ChargedAmount = 51m };
        await _funds.CommitSquareCardTopUpAsync("tab1", 50m, null, meta);
        await _funds.CommitSquareCardTopUpAsync("tab1", 50m, null, meta);
        using var conn = _factory.OpenConnection();
        Assert.Equal(50m, conn.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE LegacyId = 'tab1'"));
        Assert.Equal(1L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM Payments WHERE ExternalRef = 'k1'"));
    }

    [Fact]
    public async Task DrinkSale_AndUndo()
    {
        var r = await _drinks.CommitDrinkSaleAsync("tab1", [new TabDrinkSaleLine { ItemId = 1, DisplayName = "Cola", UnitPrice = 5m, Quantity = 1 }]);
        Assert.True(r.Ok);
        var u = await _drinks.ReverseDrinkBatchAsync("tab1", r.DrinkCommitBatchId!);
        Assert.True(u.Ok);
        using var conn = _factory.OpenConnection();
        Assert.Equal(0m, conn.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE LegacyId = 'tab1'"));
        Assert.Equal(98, conn.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = 1"));
    }

    [Fact]
    public async Task PitstopCash_DeductsStock()
    {
        var r = await _pitstop.CommitSaleAsync(
            [new PitstopSaleLineCommit { ItemId = 1, DisplayName = "Cola", UnitPrice = 5m, Quantity = 2 }],
            new PitstopSalePaymentCommit { PaymentMethod = "Cash", BaseProductTotal = 10m, ChargedTotal = 10m, CashReceived = 20m, CashChange = 10m, IdempotencyKey = "cash-guid-1" });
        Assert.True(r.Ok);
        using var conn = _factory.OpenConnection();
        Assert.Equal(98, conn.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = 1"));
    }

    [Fact]
    public async Task PitstopSale_IdempotentByTransactionGuid()
    {
        var pay = new PitstopSalePaymentCommit
        {
            PaymentMethod = "Cash",
            BaseProductTotal = 10m,
            ChargedTotal = 10m,
            CashReceived = 20m,
            CashChange = 10m,
            IdempotencyKey = "txn-dup-1",
        };
        var lines = new[] { new PitstopSaleLineCommit { ItemId = 1, DisplayName = "Cola", UnitPrice = 5m, Quantity = 2 } };
        var first = await _pitstop.CommitSaleAsync(lines, pay);
        var second = await _pitstop.CommitSaleAsync(lines, pay);
        Assert.True(first.Ok);
        Assert.True(second.Ok);
        Assert.Equal(first.SaleGuid, second.SaleGuid);
        using var conn = _factory.OpenConnection();
        Assert.Equal(1L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM PitstopSales WHERE IdempotencyKey = 'txn-dup-1'"));
        Assert.Equal(98, conn.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = 1"));
    }

    [Fact]
    public async Task SquareAttempt_TerminalApproved_SkipsSecondBeginInsert()
    {
        var key = "square-key-1";
        var begin1 = await _attempts.BeginAsync(new SquarePaymentAttemptBeginRequest
        {
            PaymentType = SquarePaymentAttemptType.PitstopSale,
            IdempotencyKey = key,
            BaseAmount = 10m,
            ChargedAmount = 10.17m,
            SurchargeAmount = 0.17m,
        });
        await _attempts.MarkTerminalApprovedAsync(begin1.AttemptId, "sq-pay-1", "chk-1");
        var begin2 = await _attempts.BeginAsync(new SquarePaymentAttemptBeginRequest
        {
            PaymentType = SquarePaymentAttemptType.PitstopSale,
            IdempotencyKey = key,
            BaseAmount = 10m,
            ChargedAmount = 10.17m,
            SurchargeAmount = 0.17m,
        });
        Assert.Equal(begin1.AttemptId, begin2.AttemptId);
        Assert.True(begin2.AlreadyTerminalApproved);
        using var conn = _factory.OpenConnection();
        Assert.Equal(1L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM SquarePaymentAttempts WHERE IdempotencyKey = @k", new { k = key }));
    }

    [Fact]
    public async Task PitstopRecovery_CommitsSaleOnce()
    {
        var key = "recovery-txn-1";
        var begin = await _attempts.BeginAsync(new SquarePaymentAttemptBeginRequest
        {
            PaymentType = SquarePaymentAttemptType.PitstopSale,
            IdempotencyKey = key,
            BaseAmount = 10m,
            ChargedAmount = 10m,
        });
        await _attempts.MarkTerminalApprovedAsync(begin.AttemptId, "sq-rec-1", null);

        var payload = new PitstopPaymentRecoveryPayload
        {
            TransactionGuid = key,
            PaymentMethod = "Card",
            Lines =
            [
                new PitstopSaleLineCommit { ItemId = 1, DisplayName = "Cola", UnitPrice = 5m, Quantity = 2 },
            ],
            Payment = new PitstopSalePaymentCommit
            {
                PaymentMethod = "Card",
                BaseProductTotal = 10m,
                ChargedTotal = 10m,
                IdempotencyKey = key,
                SquareExternalRef = "sq-rec-1",
                PaymentAttemptId = begin.AttemptId,
            },
        };
        var json = JsonSerializer.Serialize(payload);
        await _attempts.SaveRecoveryPayloadAsync(begin.AttemptId, json);

        var recover = await _paymentRecovery.RecoverPitstopSaleAsync(begin.AttemptId);
        Assert.True(recover.Ok);

        using var conn = _factory.OpenConnection();
        Assert.Equal(1L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM PitstopSales WHERE IdempotencyKey = @k", new { k = key }));
        Assert.Equal(98, conn.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = 1"));
        Assert.Equal(0, await _recovery.GetUnresolvedCountAsync());
    }

    [Fact]
    public async Task PitstopEod_Reconciliation_ExcludesBar()
    {
        await _funds.CommitFundMovementAsync("tab1", "cash", 10m, null);
        await _pitstop.CommitSaleAsync(
            [new PitstopSaleLineCommit { ItemId = 1, DisplayName = "Cola", UnitPrice = 5m, Quantity = 2 }],
            new PitstopSalePaymentCommit { PaymentMethod = "Cash", BaseProductTotal = 10m, ChargedTotal = 10m, CashReceived = 20m, CashChange = 10m });
        var recon = new PitstopEodReconciliationService(_factory, _pitstop);
        var report = await recon.BuildAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), 1.75m);
        Assert.Equal(10m, report.CashSales);
        Assert.Equal(10m, report.TotalRecordedSales);
    }

    [Fact]
    public async Task PitstopDaySales_Clear_DoesNotChangeStock()
    {
        await _pitstop.CommitSaleAsync(
            [new PitstopSaleLineCommit { ItemId = 1, DisplayName = "Cola", UnitPrice = 5m, Quantity = 2 }],
            new PitstopSalePaymentCommit { PaymentMethod = "Cash", BaseProductTotal = 10m, ChargedTotal = 10m, CashReceived = 20m, CashChange = 10m });
        using var conn = _factory.OpenConnection();
        Assert.Equal(98, conn.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = 1"));

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var end = DateTimeOffset.UtcNow.AddDays(1);
        var clear = await _pitstop.ClearPitstopRetailSalesForPeriodAsync(start, end);
        Assert.True(clear.Ok);
        Assert.Equal(1, clear.SalesRemoved);
        Assert.Equal(98, conn.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = 1"));
        Assert.Equal(0L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM PitstopSales WHERE lower(trim(COALESCE(SaleMode,''))) = 'pitstop'"));
    }

    private void Seed()
    {
        using var conn = _factory.OpenConnection();
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        conn.Execute("INSERT INTO Items (Name, StockQty, TrackStock, IsActive, CreatedAt, UpdatedAt, CatalogBucket, CatalogSubCategory) VALUES ('Cola', 100, 1, 1, @now, @now, 'Bar', 'Drinks')", new { now });
        conn.Execute("INSERT INTO Tabs (LegacyId, Name, Balance, IsGuest, TabType, CreatedAt, UpdatedAt) VALUES ('tab1', 'Test Tab', 0, 0, 'Member', @now, @now)", new { now });
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task<long> LogAsync(AuditLogEntryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task<long> LogAsync(
            string actionType,
            string? entityType = null,
            string? entityId = null,
            decimal? amount = null,
            string? reason = null,
            bool success = true,
            string? detailsJson = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);
    }
}
