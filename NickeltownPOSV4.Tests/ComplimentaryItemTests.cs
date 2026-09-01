using System.ComponentModel;
using System.Globalization;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Complimentary;
using NickeltownPOSV4.Services.Tabs;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class ComplimentaryItemTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly CaptureAudit _audit;
    private readonly SqliteComplimentaryItemRepository _repo;
    private readonly SqlitePitstopRetailSaleRepository _pitstop;

    public ComplimentaryItemTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ntpos_free_{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
        new DatabaseInitializer(_factory).InitializeAsync().GetAwaiter().GetResult();
        _audit = new CaptureAudit();
        _repo = new SqliteComplimentaryItemRepository(_factory, _audit);
        _pitstop = new SqlitePitstopRetailSaleRepository(_factory, new SqliteSquarePaymentAttemptRepository(_factory));
        SeedCatalog();
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
    public async Task FreeWater_DecreasesWaterStockByOne_AndDoesNotChangePitstopPrice()
    {
        var before = StockQty(WaterId);
        var result = await Record(WaterId);
        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(ComplimentaryTransactionTypes.ComplimentaryItem, LastTransactionType());
        Assert.Equal(before - 1, StockQty(WaterId));
        Assert.Equal(2.00m, PitstopPrice(WaterId));
    }

    [Fact]
    public async Task FreePopTop_DecreasesPopTopStockByOne()
    {
        var before = StockQty(PopTopId);
        var result = await Record(PopTopId);
        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(before - 1, StockQty(PopTopId));
    }

    [Fact]
    public async Task FreeItem_DoesNotAffectMemberBalance_AndDoesNotRequireATab()
    {
        InsertMemberTab("Smith", 25.50m);
        var result = await Record(WaterId);
        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(25.50m, TabBalance("Smith"));
        using var conn = _factory.OpenConnection();
        Assert.Equal(0L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM TabEntries"));
        Assert.Equal(0L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM ComplimentaryItemIssues WHERE StaffName = 'Free Items'"));
    }

    [Fact]
    public async Task PaidPitstopSale_StillUsesNormalProductPrice_AndSharesStockWithFreeIssue()
    {
        Assert.Equal(30, StockQty(WaterId));
        var free = await Record(WaterId);
        Assert.True(free.Ok, free.ErrorMessage);
        Assert.Equal(29, StockQty(WaterId));

        var sale = await _pitstop.CommitSaleAsync(
            [new PitstopSaleLineCommit
            {
                ItemId = WaterId,
                DisplayName = "Water",
                UnitPrice = 2.00m,
                Quantity = 1,
            }],
            new PitstopSalePaymentCommit
            {
                PaymentMethod = "Cash",
                BaseProductTotal = 2.00m,
                ChargedTotal = 2.00m,
                StaffDisplayName = "Dylan",
            });
        Assert.True(sale.Ok, sale.ErrorMessage);
        Assert.Equal(28, StockQty(WaterId));
        Assert.Equal(2.00m, PitstopPrice(WaterId));

        using var conn = _factory.OpenConnection();
        var saleTotal = conn.ExecuteScalar<decimal>("SELECT Total FROM PitstopSales WHERE Id = @id", new { id = sale.SalePk });
        Assert.Equal(2.00m, saleTotal);
    }

    [Fact]
    public async Task Undo_RestoresStock_DoesNotAffectMemberBalance_AndIsIdempotent()
    {
        InsertMemberTab("Smith", 12m);
        var recorded = await Record(WaterId);
        Assert.True(recorded.Ok, recorded.ErrorMessage);
        Assert.Equal(29, StockQty(WaterId));

        var undone = await _repo.ReverseAsync(recorded.IssueGuid!, 1, "Dylan");
        Assert.True(undone.Ok, undone.ErrorMessage);
        Assert.False(undone.AlreadyReversed);
        Assert.Equal(30, StockQty(WaterId));
        Assert.Equal(12m, TabBalance("Smith"));
        Assert.Equal(0, await _repo.GetTodayCountAsync(WaterId, DateOnly.FromDateTime(DateTime.Now)));

        var again = await _repo.ReverseAsync(recorded.IssueGuid!, 1, "Dylan");
        Assert.True(again.Ok, again.ErrorMessage);
        Assert.True(again.AlreadyReversed);
        Assert.Equal(30, StockQty(WaterId));
    }

    [Fact]
    public async Task TodayCounter_IsDerivedFromActiveTransactions()
    {
        for (var i = 0; i < 3; i++)
        {
            Assert.True((await Record(WaterId)).Ok);
        }

        for (var i = 0; i < 2; i++)
        {
            Assert.True((await Record(PopTopId)).Ok);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        Assert.Equal(3, await _repo.GetTodayCountAsync(WaterId, today));
        Assert.Equal(2, await _repo.GetTodayCountAsync(PopTopId, today));

        var lastWater = await Record(WaterId);
        await _repo.ReverseAsync(lastWater.IssueGuid!, 1, "Dylan");
        Assert.Equal(3, await _repo.GetTodayCountAsync(WaterId, today));
    }

    [Fact]
    public async Task Configuration_ControlsWhichProductsAppear_AndRemoveDoesNotDeleteProduct()
    {
        Assert.Empty(await _repo.GetButtonsAsync(DateOnly.FromDateTime(DateTime.Now)));

        Assert.True((await _repo.AddConfigAsync(WaterId, "WATER", "W")).Ok);
        Assert.True((await _repo.AddConfigAsync(PopTopId, "POP TOP", "P")).Ok);

        var buttons = await _repo.GetButtonsAsync(DateOnly.FromDateTime(DateTime.Now));
        Assert.Equal(2, buttons.Count);
        Assert.Contains(buttons, b => b.ItemId == WaterId && b.DisplayLabel == "WATER" && b.Icon == "W");
        Assert.Contains(buttons, b => b.ItemId == PopTopId && b.DisplayLabel == "POP TOP");

        Assert.True((await _repo.RemoveConfigAsync(WaterId)).Ok);
        Assert.Equal("Water", ItemName(WaterId));
        Assert.Equal(30, StockQty(WaterId));
        buttons = await _repo.GetButtonsAsync(DateOnly.FromDateTime(DateTime.Now));
        Assert.Single(buttons);
        Assert.Equal(PopTopId, buttons[0].ItemId);
    }

    [Fact]
    public async Task DuplicateIdempotencyKey_DoesNotRecordTwice()
    {
        var key = Guid.NewGuid().ToString("N");
        var first = await Record(PopTopId, key);
        var second = await Record(PopTopId, key);
        Assert.True(first.Ok, first.ErrorMessage);
        Assert.True(second.Ok, second.ErrorMessage);
        Assert.True(second.AlreadyRecorded);
        Assert.Equal(49, StockQty(PopTopId));
        using var conn = _factory.OpenConnection();
        Assert.Equal(1L, conn.ExecuteScalar<long>("SELECT COUNT(1) FROM ComplimentaryItemIssues WHERE ItemId = @id", new { id = PopTopId }));
    }

    [Fact]
    public async Task RapidIntentionalEntries_WithDistinctKeys_AreAllRecorded()
    {
        for (var i = 0; i < 3; i++)
        {
            var result = await Record(PopTopId, Guid.NewGuid().ToString("N"));
            Assert.True(result.Ok, result.ErrorMessage);
            Assert.False(result.AlreadyRecorded);
        }

        Assert.Equal(47, StockQty(PopTopId));
        Assert.Equal(3, await _repo.GetTodayCountAsync(PopTopId, DateOnly.FromDateTime(DateTime.Now)));
    }

    [Fact]
    public void TapGuard_BlocksAccidentalDoubleTap_ThenAllowsIntentionalRepeat()
    {
        var guard = new ComplimentaryItemTapGuard();
        Assert.True(guard.TryBegin(WaterId, milliseconds: 10_000));
        Assert.False(guard.TryBegin(WaterId, milliseconds: 10_000));
        Assert.True(guard.TryBegin(PopTopId, milliseconds: 10_000));
        guard.End(WaterId);
        Assert.False(guard.TryBegin(WaterId, milliseconds: 10_000));
        guard.End(PopTopId);

        var shortGuard = new ComplimentaryItemTapGuard();
        Assert.True(shortGuard.TryBegin(WaterId, milliseconds: 1));
        shortGuard.End(WaterId);
        Thread.Sleep(20);
        Assert.True(shortGuard.TryBegin(WaterId, milliseconds: 1));
    }

    [Fact]
    public async Task OutOfStock_IsRejected_AndDoesNotBypassStock()
    {
        SetStock(WaterId, 0);
        var result = await Record(WaterId);
        Assert.False(result.Ok);
        Assert.Equal(0, StockQty(WaterId));
        await _repo.AddConfigAsync(WaterId, "WATER", "W");
        var buttons = await _repo.GetButtonsAsync(DateOnly.FromDateTime(DateTime.Now));
        var water = Assert.Single(buttons);
        Assert.Equal(0, water.StockQty);
        Assert.Equal(1, water.TrackStock);
    }

    [Fact]
    public async Task Report_CountsActiveComplimentaryItems_WithInformationalRetailValue()
    {
        await _repo.AddConfigAsync(WaterId, "WATER", "W");
        await _repo.AddConfigAsync(PopTopId, "POP TOP", "P");
        for (var i = 0; i < 7; i++)
        {
            Assert.True((await Record(WaterId)).Ok);
        }

        for (var i = 0; i < 11; i++)
        {
            Assert.True((await Record(PopTopId)).Ok);
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var report = await _repo.GetReportAsync(today, today);
        Assert.Equal(18, report.TotalItems);
        var water = Assert.Single(report.Lines, l => l.ItemId == WaterId);
        var pop = Assert.Single(report.Lines, l => l.ItemId == PopTopId);
        Assert.Equal(7, water.Quantity);
        Assert.Equal(11, pop.Quantity);
        Assert.Equal(14.00m, water.RetailValue);
        Assert.Equal(27.50m, pop.RetailValue);
        Assert.Equal(41.50m, report.TotalRetailValue);
    }

    [Fact]
    public async Task Audit_RecordsIssueUndoAndConfigChanges()
    {
        Assert.True((await _repo.AddConfigAsync(WaterId, "WATER", "W")).Ok);
        var recorded = await Record(WaterId);
        Assert.True(recorded.Ok, recorded.ErrorMessage);
        Assert.True((await _repo.ReverseAsync(recorded.IssueGuid!, 1, "Dylan")).Ok);
        Assert.True((await _repo.UpdateConfigAsync(WaterId, "WATER", "WW")).Ok);
        Assert.True((await _repo.RemoveConfigAsync(WaterId)).Ok);

        Assert.Contains(_audit.Entries, e => e.ActionType == AuditActions.QuickFreeItemAdded);
        Assert.Contains(_audit.Entries, e => e.ActionType == AuditActions.ComplimentaryItemRecorded && e.Amount == 0m);
        Assert.Contains(_audit.Entries, e => e.ActionType == AuditActions.ComplimentaryItemUndone);
        Assert.Contains(_audit.Entries, e => e.ActionType == AuditActions.QuickFreeItemConfigChanged);
        Assert.Contains(_audit.Entries, e => e.ActionType == AuditActions.QuickFreeItemRemoved);
        Assert.All(
            _audit.Entries.Where(e => e.ActionType == AuditActions.ComplimentaryItemRecorded),
            e => Assert.False(string.IsNullOrWhiteSpace(e.EntityId)));
    }

    [Fact]
    public async Task ServiceUndoStack_ReversesOnce()
    {
        var undo = new TabWorkspaceUndoStack();
        var service = new ComplimentaryItemService(_repo, new FakeSession(), new NullCatalogCache(), undo);
        var recorded = await service.RecordAsync(WaterId, 1, Guid.NewGuid().ToString("N"));
        Assert.True(recorded.Ok, recorded.ErrorMessage);
        Assert.True(undo.CanUndo);
        Assert.Equal(TabUndoPreview.ComplimentaryActionKind, undo.Preview?.ActionKind);
        Assert.Equal(29, StockQty(WaterId));

        Assert.True(await undo.TryUndoAsync());
        Assert.False(undo.CanUndo);
        Assert.Equal(30, StockQty(WaterId));
        Assert.False(await undo.TryUndoAsync());
        Assert.Equal(30, StockQty(WaterId));
    }

    private const long WaterId = 1;
    private const long PopTopId = 2;

    private void SeedCatalog()
    {
        using var conn = _factory.OpenConnection();
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        conn.Execute(
            """
            INSERT INTO Items (Name, StockQty, TrackStock, IsActive, CreatedAt, UpdatedAt, CatalogBucket, CatalogSubCategory, ShowInBar, ShowInPitstop)
            VALUES
              ('Water', 30, 1, 1, @now, @now, 'Shared', 'Drinks', 1, 1),
              ('Pop Top', 50, 1, 1, @now, @now, 'Shared', 'Drinks', 1, 1)
            """,
            new { now });
        conn.Execute(
            """
            INSERT INTO ItemPrices (ItemId, Price, EffectiveFrom, CreatedAt, PriceKind)
            VALUES
              (1, 0, @now, @now, 'Bar'),
              (1, 2.00, @now, @now, 'Pitstop'),
              (2, 0, @now, @now, 'Bar'),
              (2, 2.50, @now, @now, 'Pitstop')
            """,
            new { now });
    }

    private Task<ComplimentaryRecordResult> Record(long itemId, string? key = null) =>
        _repo.RecordAsync(new ComplimentaryRecordRequest
        {
            ItemId = itemId,
            Quantity = 1,
            StaffId = 1,
            StaffName = "Dylan",
            IdempotencyKey = key ?? Guid.NewGuid().ToString("N"),
        });

    private int StockQty(long itemId)
    {
        using var conn = _factory.OpenConnection();
        return conn.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @id", new { id = itemId });
    }

    private void SetStock(long itemId, int qty)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute("UPDATE Items SET StockQty = @qty WHERE Id = @id", new { qty, id = itemId });
    }

    private decimal PitstopPrice(long itemId)
    {
        using var conn = _factory.OpenConnection();
        return conn.ExecuteScalar<decimal>(
            """
            SELECT Price FROM ItemPrices
            WHERE ItemId = @id AND lower(trim(PriceKind)) = 'pitstop'
            ORDER BY Id DESC LIMIT 1
            """,
            new { id = itemId });
    }

    private string ItemName(long itemId)
    {
        using var conn = _factory.OpenConnection();
        return conn.ExecuteScalar<string>("SELECT Name FROM Items WHERE Id = @id", new { id = itemId }) ?? string.Empty;
    }

    private string LastTransactionType()
    {
        using var conn = _factory.OpenConnection();
        return conn.ExecuteScalar<string>(
            "SELECT TransactionType FROM ComplimentaryItemIssues ORDER BY Id DESC LIMIT 1") ?? string.Empty;
    }

    private void InsertMemberTab(string name, decimal balance)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(
            """
            INSERT INTO Tabs (LegacyId, Name, DisplayName, Balance, IsMember, IsGuest, TabType, IsArchived, CreatedAt, UpdatedAt)
            VALUES (@legacy, @name, @name, @balance, 1, 0, 'Member', 0, datetime('now'), datetime('now'))
            """,
            new { legacy = "tab_" + name.ToLowerInvariant(), name, balance });
    }

    private decimal TabBalance(string name)
    {
        using var conn = _factory.OpenConnection();
        return conn.ExecuteScalar<decimal>("SELECT Balance FROM Tabs WHERE Name = @name LIMIT 1", new { name });
    }

    private sealed class CaptureAudit : IAuditLogService
    {
        public List<AuditLogEntryRequest> Entries { get; } = [];

        public Task<long> LogAsync(AuditLogEntryRequest request, CancellationToken cancellationToken = default)
        {
            Entries.Add(request);
            return Task.FromResult((long)Entries.Count);
        }

        public Task<long> LogAsync(
            string actionType,
            string? entityType = null,
            string? entityId = null,
            decimal? amount = null,
            string? reason = null,
            bool success = true,
            string? detailsJson = null,
            CancellationToken cancellationToken = default) =>
            LogAsync(
                new AuditLogEntryRequest
                {
                    ActionType = actionType,
                    EntityType = entityType,
                    EntityId = entityId,
                    Amount = amount,
                    Reason = reason,
                    Success = success,
                    DetailsJson = detailsJson,
                },
                cancellationToken);

        public Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(
            int maxEntries = 400,
            bool staffFacingOnly = true,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AuditLogEntry>>([]);
    }

    private sealed class FakeSession : IUserSessionService
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public bool IsSignedIn => true;

        public long? ActiveStaffId => 1;

        public string? ActiveStaffLegacyId => "dylan";

        public string? DisplayName => "Dylan";

        public string? Role => "Admin";

        public bool IsAdmin => true;

        public bool IsTreasurer => false;

        public bool IsManager => true;

        public bool CanAccessAdmin => true;

        public bool CanAccessReports => true;

        public bool CanAccessTreasurer => true;

        public bool IsDeveloper => true;

        public void SetSignedIn(long staffPk, string? legacyId, string displayName, string? role, bool isDeveloper = false)
        {
        }

        public void Clear()
        {
        }
    }

    private sealed class NullCatalogCache : IBarCatalogCache
    {
        public void Invalidate()
        {
        }

        public Task<BarCatalogSnapshot> GetOrLoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BarCatalogSnapshot { CategoryNames = [], Products = [] });
    }
}
