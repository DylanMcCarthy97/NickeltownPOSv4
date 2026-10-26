using Dapper;
using Microsoft.Data.Sqlite;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Stock;
using NickeltownPOSV4.ViewModels;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class InventoryReliabilityTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"ntpos_inventory_{Guid.NewGuid():N}.db");
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteStockEditingService _stock;

    public InventoryReliabilityTests()
    {
        _factory = new SqliteConnectionFactory(_path);
        new DatabaseInitializer(_factory).InitializeAsync().GetAwaiter().GetResult();
        _stock = new SqliteStockEditingService(_factory);
    }

    private long Seed(string name, int quantity, int active = 1, int tracked = 1)
    {
        using var db = _factory.OpenConnection();
        return db.ExecuteScalar<long>("""
            INSERT INTO Items (Name, StockQty, IsActive, TrackStock, CatalogBucket, CatalogSubCategory, StockMode,
                PreferredStockLevel, WarnMeBelow, ShowOnShoppingList, CreatedAt, UpdatedAt)
            VALUES (@name, @quantity, @active, @tracked, 'Bar', 'Drinks', @mode, 12, 3, 1, datetime('now'), datetime('now'));
            SELECT last_insert_rowid();
            """, new { name, quantity, active, tracked, mode = tracked == 1 ? "Tracked" : "NotTracked" });
    }

    private StockManagementPageViewModel ViewModel() => new(_stock, new StockItemAdminPersistenceService(_stock),
        null!, new Cache(), null!, new TabWorkspaceRefreshBus(), new Images());

    [Fact]
    public async Task SummaryCountsRefresh_AndRemainCatalogWideWhileSearching()
    {
        Seed("Empty", 0); Seed("Low", 2); Seed("Plenty", 20); Seed("Hidden", 0, active: 0); Seed("Untracked", 0, tracked: 0);
        var vm = ViewModel();
        Assert.True(await vm.LoadAsync());
        Assert.Equal("4", vm.SummaryTotalItemsText);
        Assert.Equal("2", vm.SummaryNeedBuyingText);
        Assert.Equal("1", vm.SummaryOutOfStockText);
        vm.SearchText = "does not exist";
        Assert.True(vm.ShowStockEmptyState);
        Assert.Equal("4", vm.SummaryTotalItemsText);
        vm.ResetStockFiltersCommand.Execute(null);
        Assert.Equal(4, vm.PageRows.Count);
        Assert.False(vm.ShowStockEmptyState);
        Assert.Equal("—", vm.PageRows.Single(r => r.Name == "Untracked").StockHeroText);
        Assert.Equal("Not tracked", vm.PageRows.Single(r => r.Name == "Untracked").StatusText);
    }

    [Fact]
    public async Task AdjustmentScreenSavesFreshQuantity_InsteadOfReportingFalseSuccess()
    {
        var id = Seed("Lager", 10);
        var vm = ViewModel();
        await vm.LoadAsync();
        vm.SelectRowCommand.Execute(vm.PageRows.Single());
        using var db = _factory.OpenConnection();
        db.Execute("UPDATE Items SET StockQty = 7 WHERE Id = @id", new { id }); // Sold while the editor was open.
        vm.AdjustMode = "Decrease"; vm.AdjustQuantityText = "2"; vm.AdjustReason = "Damaged bottles";
        Assert.True(await vm.ApplySelectedStockAdjustmentAsync(), vm.StatusMessage);
        Assert.Equal(5, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @id", new { id }));
        Assert.Equal(-2, db.ExecuteScalar<int>("SELECT DeltaQty FROM StockMovements WHERE ItemId = @id", new { id }));
        Assert.Equal("Damaged bottles", db.ExecuteScalar<string>("SELECT Reason FROM StockMovements WHERE ItemId = @id", new { id }));
        Assert.Contains("Stock saved", vm.StatusMessage);
        Assert.Equal("Lager", db.ExecuteScalar<string>("SELECT Name FROM Items WHERE Id = @id", new { id }));
    }

    [Fact]
    public async Task AdjustmentToZeroWorks_AndExcessDecreaseDoesNotSilentlyClamp()
    {
        var id = Seed("Lager", 3);
        var vm = ViewModel();
        await vm.LoadAsync();
        vm.SelectRowCommand.Execute(vm.PageRows.Single());
        Assert.False(await vm.ApplyQuickStockDeltaAndPersistCoreAsync(-6));
        using var db = _factory.OpenConnection();
        Assert.Equal(3, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @id", new { id }));
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM StockMovements"));
        vm.AdjustMode = "Set To"; vm.AdjustQuantityText = "0"; vm.AdjustReason = "Physical count";
        Assert.True(await vm.ApplySelectedStockAdjustmentAsync(), vm.StatusMessage);
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @id", new { id }));
    }

    [Fact]
    public async Task ReceivingUsesCurrentStock_AndRecordsMatchingPurchaseCost()
    {
        var id = Seed("Lager", 10);
        var vm = ViewModel(); await vm.LoadAsync();
        using var db = _factory.OpenConnection();
        db.Execute("UPDATE Items SET StockQty = 7 WHERE Id = @id", new { id });
        Assert.True(await vm.ReceiveStockAsync(id, 2, 6, 30), vm.StatusMessage);
        Assert.Equal(19, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @id", new { id }));
        Assert.Equal(12, db.ExecuteScalar<int>("SELECT DeltaQty FROM StockMovements WHERE ItemId = @id", new { id }));
        Assert.Equal(2.5m, db.ExecuteScalar<decimal>("SELECT CostEach FROM StockPurchases WHERE ItemId = @id", new { id }));
        Assert.Equal(2.5m, db.ExecuteScalar<decimal>("SELECT CostPrice FROM Items WHERE Id = @id", new { id }));
    }

    [Fact]
    public async Task FailedReceiveValidationDoesNotChangeStockOrRecordPurchase()
    {
        var id = Seed("Lager", 10);
        await Assert.ThrowsAsync<ArgumentException>(() => _stock.ReceiveStockAsync(new StockPurchaseWrite
            { ItemId = id, PacksBought = 2, ItemsPerPack = 6, TotalItems = 99, TotalPaid = 30, CostEach = 1 }));
        var vm = ViewModel(); await vm.LoadAsync();
        Assert.False(await vm.ReceiveStockAsync(id, int.MaxValue, 2, 30));
        using var db = _factory.OpenConnection();
        Assert.Equal(10, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @id", new { id }));
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM StockPurchases"));
    }

    [Fact]
    public async Task CountConflictRollsBackEntireBatch_AndDoesNotStampCountDate()
    {
        var first = Seed("Lager", 10); var second = Seed("Cola", 5);
        using var db = _factory.OpenConnection();
        db.Execute("UPDATE Items SET StockQty = 4 WHERE Id = @second", new { second });
        await Assert.ThrowsAsync<InvalidOperationException>(() => _stock.ApplyStockCountAsync(
            [(first, 8), (second, 3)], [first, second], expectedQuantities: new Dictionary<long, int> { [first] = 10, [second] = 5 }));
        Assert.Equal(10, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @first", new { first }));
        Assert.Equal(4, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @second", new { second }));
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM Items WHERE LastStockCountDate IS NOT NULL"));
        Assert.Equal(0, db.ExecuteScalar<int>("SELECT COUNT(*) FROM StockMovements"));
    }

    [Fact]
    public async Task CountOnlyStampsCountedItems_AndRejectsNegativeOrDuplicateRows()
    {
        var first = Seed("Lager", 10); var other = Seed("Cola", 5);
        await _stock.ApplyStockCountAsync([(first, 8)], [first], expectedQuantities: new Dictionary<long, int> { [first] = 10 });
        using var db = _factory.OpenConnection();
        Assert.NotNull(db.ExecuteScalar<string?>("SELECT LastStockCountDate FROM Items WHERE Id = @first", new { first }));
        Assert.Null(db.ExecuteScalar<string?>("SELECT LastStockCountDate FROM Items WHERE Id = @other", new { other }));
        await Assert.ThrowsAsync<ArgumentException>(() => _stock.ApplyStockCountAsync([(first, -1)]));
        await Assert.ThrowsAsync<ArgumentException>(() => _stock.ApplyStockCountAsync([(first, 5), (first, 2)]));
        Assert.Equal(8, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @first", new { first }));
    }

    [Fact]
    public async Task StaleProductEditCannotOverwriteInterveningSale()
    {
        var id = Seed("Lager", 7);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _stock.UpdateItemAdminAsync(new StockItemAdminWrite
            { ItemId = id, Name = "Edited", StockQty = 10, ExpectedStockQty = 10, TrackStock = 1,
                StockMode = StockCatalogTaxonomy.StockModeTracked, CatalogBucket = "Bar", CatalogSubCategory = "Drinks" }));
        using var db = _factory.OpenConnection();
        Assert.Equal(7, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @id", new { id }));
        Assert.Equal("Lager", db.ExecuteScalar<string>("SELECT Name FROM Items WHERE Id = @id", new { id }));
    }

    [Fact]
    public async Task RefreshingListDoesNotRelaxConflictGuardForAnOpenProductEditor()
    {
        var id = Seed("Lager", 10);
        using var db = _factory.OpenConnection();
        db.Execute("INSERT INTO ItemPrices (ItemId, Price, PriceKind, EffectiveFrom, CreatedAt) VALUES (@id, 5, 'Bar', datetime('now'), datetime('now'))", new { id });
        var vm = ViewModel();
        await vm.LoadAsync();
        vm.SelectRowCommand.Execute(vm.PageRows.Single());
        Assert.Equal("10", vm.DetailStockText);
        db.Execute("UPDATE Items SET StockQty = 7 WHERE Id = @id", new { id });
        await vm.LoadAsync();
        Assert.Equal("10", vm.DetailStockText); // Unsaved editor still shows the original snapshot.
        Assert.False(await vm.TryPersistCurrentItemAsync());
        Assert.Contains("Stock changed", vm.StatusMessage);
        Assert.Equal(7, db.ExecuteScalar<int>("SELECT StockQty FROM Items WHERE Id = @id", new { id }));
        vm.SelectRowCommand.Execute(vm.PageRows.Single()); // Deliberately reopen and review the refreshed item.
        Assert.Equal("7", vm.DetailStockText);
    }

    public void Dispose() { SqliteConnection.ClearAllPools(); try { File.Delete(_path); } catch (IOException) { } }

    private sealed class Cache : IBarCatalogCache
    {
        public void Invalidate() { }
        public Task<BarCatalogSnapshot> GetOrLoadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Images : IStockProductImageStorage
    {
        public string ProductImagesDirectory => string.Empty;
        public bool IsManagedPath(string? path) => false;
        public ProductImageImportResult ImportFromSource(string sourcePath, long? itemId) => throw new NotSupportedException();
        public string? EnsureItemIdFileName(string? storedPath, long itemId) => storedPath;
        public string? ResolveImportImagePath(string? imagePath, long itemId) => imagePath;
    }
}
