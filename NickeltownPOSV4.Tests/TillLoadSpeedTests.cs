using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Tabs;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class TillLoadSpeedTests
{
    [Fact]
    public void CatalogPulse_DoesNotRebuildTheTabBoard()
    {
        var bus = new TabWorkspaceRefreshBus();
        var board = 0;
        var catalog = 0;
        bus.RefreshRequested += (_, _) => board++;
        bus.CatalogRefreshRequested += (_, _) => catalog++;
        var cache = new FakeBarCatalogCache();
        var service = new PosCatalogAutoRefreshService(bus, cache);

        service.PulseRefresh();

        Assert.Equal(0, board);
        Assert.Equal(1, catalog);
        Assert.True(cache.Invalidated);
        Assert.Equal(TimeSpan.FromSeconds(90), PosCatalogAutoRefreshService.RefreshInterval);
    }

    [Fact]
    public void MoneyRefresh_StillReloadsBoardAndCatalog()
    {
        var bus = new TabWorkspaceRefreshBus();
        var board = 0;
        var catalog = 0;
        bus.RefreshRequested += (_, _) => board++;
        bus.CatalogRefreshRequested += (_, _) => catalog++;

        bus.RequestRefresh();

        Assert.Equal(1, board);
        Assert.Equal(1, catalog);
    }

    [Fact]
    public void BoardSnapshot_IgnoresUnchangedTabs()
    {
        var first = new TabCardModel("tab-1", "Smith", 12.50m, "Beer", lastActivityAt: "2026-11-01", openDrinkCount: 2);
        var same = new TabCardModel("tab-1", "Smith", 12.50m, "Beer", lastActivityAt: "2026-11-01", openDrinkCount: 2);
        var changed = new TabCardModel("tab-1", "Smith", 10.00m, "Beer", lastActivityAt: "2026-11-01", openDrinkCount: 2);

        Assert.Equal(TabsBoardSnapshot.Fingerprint([first]), TabsBoardSnapshot.Fingerprint([same]));
        Assert.NotEqual(TabsBoardSnapshot.Fingerprint([first]), TabsBoardSnapshot.Fingerprint([changed]));
    }

    [Fact]
    public async Task SchemaInit_StoresVersionSoLaterStartsSkipTheGrind()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ntpos_schema_{Guid.NewGuid():N}.db");
        try
        {
            var factory = new SqliteConnectionFactory(path);
            var initializer = new DatabaseInitializer(factory);
            await initializer.InitializeAsync();
            using (var conn = factory.OpenConnection())
            {
                Assert.Equal(DatabaseInitializer.CurrentSchemaVersion, conn.ExecuteScalar<long>("PRAGMA user_version"));
            }

            await initializer.InitializeAsync();
            using (var conn = factory.OpenConnection())
            {
                Assert.Equal(DatabaseInitializer.CurrentSchemaVersion, conn.ExecuteScalar<long>("PRAGMA user_version"));
                Assert.Equal(1, conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'Tabs'"));
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    private sealed class FakeBarCatalogCache : IBarCatalogCache
    {
        public bool Invalidated { get; private set; }

        public void Invalidate() => Invalidated = true;

        public Task<BarCatalogSnapshot> GetOrLoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BarCatalogSnapshot
            {
                CategoryNames = [],
                Products = [],
            });
    }
}
