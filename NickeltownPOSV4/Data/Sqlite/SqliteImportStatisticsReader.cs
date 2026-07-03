using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteImportStatisticsReader : IImportDatabaseStatistics
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteImportStatisticsReader(SqliteConnectionFactory factory) => _factory = factory;

    public Task<ImportVerificationSnapshot> BuildVerificationAsync(
        MigrationImportResult? lastRun,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();

        var tabsOpen = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM Tabs WHERE IsArchived = 0 AND COALESCE(IsDeleted,0) = 0;");
        var tabsArchived = conn.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM Tabs WHERE IsArchived != 0 AND COALESCE(IsDeleted,0) = 0;");
        var tabEntries = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM TabEntries;");
        var items = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Items;");
        var bartenders = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Bartenders;");
        var members = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM Members;");
        var pitstop = conn.ExecuteScalar<long>("SELECT COUNT(*) FROM PitstopSales;");

        var sampleRows = conn.Query<SampleRow>(
            new CommandDefinition(
                """
                SELECT
                  COALESCE(NULLIF(DisplayName, ''), Name, LegacyId) AS TabName,
                  Balance,
                  COALESCE(LastDrinkSummary, '') AS LastActivity,
                  CASE WHEN IsMember != 0 THEN 'Member' ELSE 'Guest' END AS MemberOrGuest,
                  CASE WHEN IsArchived != 0 THEN 'Archived' ELSE 'Open' END AS OpenOrArchived,
                  COALESCE(LegacyId, '') AS LegacyId,
                  COALESCE(LegacyKey, LegacyId, '') AS LegacyKey
                FROM Tabs
                ORDER BY datetime(UpdatedAt) DESC
                LIMIT 5
                """,
                cancellationToken: cancellationToken)).ToList();

        var samples = new List<ImportedTabVerificationRow>(sampleRows.Count);
        foreach (var r in sampleRows)
        {
            var member = r.MemberOrGuest ?? "—";
            var status = r.OpenOrArchived ?? "—";
            var legacyId = r.LegacyId ?? "—";
            var legacyKey = r.LegacyKey ?? "—";
            samples.Add(new ImportedTabVerificationRow
            {
                TabName = r.TabName ?? "(unnamed)",
                Balance = r.Balance,
                BalanceText = $"Balance: {r.Balance.ToString("F2", CultureInfo.CurrentCulture)}",
                LastActivity = string.IsNullOrEmpty(r.LastActivity) ? "—" : r.LastActivity,
                MemberOrGuest = member,
                OpenOrArchived = status,
                MemberAndStatusLine = $"{member} · {status}",
                LegacyId = legacyId,
                LegacyKey = legacyKey,
                LegacyLine = $"LegacyId: {legacyId} · LegacyKey: {legacyKey}",
            });
        }

        var itemSampleRows = conn.Query<ItemSampleRow>(
            new CommandDefinition(
                """
                SELECT Name, ItemType, StockQty, COALESCE(LegacyId, '') AS LegacyId
                FROM Items
                ORDER BY datetime(UpdatedAt) DESC
                LIMIT 5
                """,
                cancellationToken: cancellationToken)).ToList();

        var itemSamples = new List<ImportedItemVerificationRow>(itemSampleRows.Count);
        foreach (var ir in itemSampleRows)
        {
            var legacy = string.IsNullOrEmpty(ir.LegacyId) ? "—" : ir.LegacyId;
            itemSamples.Add(new ImportedItemVerificationRow
            {
                Name = ir.Name ?? "(unnamed)",
                ItemType = ir.ItemType ?? "Item",
                StockQty = ir.StockQty,
                LegacyId = legacy,
                DetailLine = $"{ir.ItemType ?? "Item"} · stock {ir.StockQty} · LegacyId {legacy}",
            });
        }

        var runImported = lastRun?.Segments.Sum(s => s.Imported) ?? 0;
        var runSkipped = lastRun?.Segments.Sum(s => s.SkippedDuplicate) ?? 0;
        var runFailed = (lastRun?.Segments.Sum(s => s.Failures.Count) ?? 0) + (lastRun?.GlobalFailures.Count ?? 0);

        return Task.FromResult(new ImportVerificationSnapshot
        {
            TabsOpenCount = (int)tabsOpen,
            TabsArchivedCount = (int)tabsArchived,
            TabEntriesCount = (int)tabEntries,
            ItemsCount = (int)items,
            BartendersCount = (int)bartenders,
            MembersCount = (int)members,
            PitstopSalesCount = (int)pitstop,
            RunRecordsImported = runImported,
            RunFilesSkippedDuplicate = runSkipped,
            RunFailedRecords = runFailed,
            SampleTabs = samples,
            SampleItems = itemSamples,
        });
    }

    private sealed class ItemSampleRow
    {
        public string? Name { get; set; }

        public string? ItemType { get; set; }

        public int StockQty { get; set; }

        public string? LegacyId { get; set; }
    }

    private sealed class SampleRow
    {
        public string? TabName { get; set; }

        public decimal Balance { get; set; }

        public string? LastActivity { get; set; }

        public string? MemberOrGuest { get; set; }

        public string? OpenOrArchived { get; set; }

        public string? LegacyId { get; set; }

        public string? LegacyKey { get; set; }
    }
}
