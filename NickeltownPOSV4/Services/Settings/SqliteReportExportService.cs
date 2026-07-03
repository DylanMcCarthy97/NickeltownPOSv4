using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services.Settings;

public sealed class SqliteReportExportService : IReportExportService
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteReportExportService(SqliteConnectionFactory factory) => _factory = factory;

    public Task<byte[]> BuildMonthlyTabsCsvAsync(DateTime month, CancellationToken cancellationToken = default) =>
        Task.Run(() => BuildMonthlyTabsCsv(month), cancellationToken);

    public Task<byte[]> BuildMonthlyTabsPdfAsync(DateTime month, CancellationToken cancellationToken = default) =>
        Task.Run(() => MonthlyTabsPdfBuilder.Build(_factory, month), cancellationToken);

    public Task<byte[]> BuildStockSnapshotCsvAsync(CancellationToken cancellationToken = default) =>
        Task.Run(BuildStockSnapshotCsv, cancellationToken);

    public Task<byte[]> BuildStockSnapshotPdfAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => StockSnapshotPdfBuilder.Build(_factory), cancellationToken);

    public Task<IReadOnlyList<ArchivedTabListRow>> ListArchivedTabsAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<ArchivedTabListRow>>(ListArchivedTabs, cancellationToken);

    private byte[] BuildMonthlyTabsCsv(DateTime month)
    {
        var monthStart = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var monthEnd = monthStart.AddMonths(1);

        using var conn = _factory.OpenConnection();

        var entryRows = conn.Query<MonthlyEntryRow>(
            """
            SELECT
              t.Id              AS TabId,
              COALESCE(t.DisplayName, t.Name) AS TabName,
              t.TabType         AS TabType,
              e.EntryType       AS Kind,
              e.Amount          AS Amount,
              e.Note            AS Note,
              COALESCE(e.OccurredAt, e.CreatedAt) AS OccurredAt
            FROM TabEntries e
            JOIN Tabs t ON t.Id = e.TabId
            WHERE COALESCE(e.OccurredAt, e.CreatedAt) >= @Start
              AND COALESCE(e.OccurredAt, e.CreatedAt) <  @End
            """,
            new { Start = monthStart.ToString("o"), End = monthEnd.ToString("o") })
            .ToList();

        var movementRows = conn.Query<MonthlyEntryRow>(
            """
            SELECT
              t.Id              AS TabId,
              COALESCE(t.DisplayName, t.Name) AS TabName,
              t.TabType         AS TabType,
              m.MovementType    AS Kind,
              m.Amount          AS Amount,
              m.Note            AS Note,
              COALESCE(m.OccurredAt, m.CreatedAt) AS OccurredAt
            FROM MoneyMovements m
            JOIN Tabs t ON t.Id = m.TabId
            WHERE m.TabId IS NOT NULL
              AND COALESCE(m.OccurredAt, m.CreatedAt) >= @Start
              AND COALESCE(m.OccurredAt, m.CreatedAt) <  @End
            """,
            new { Start = monthStart.ToString("o"), End = monthEnd.ToString("o") })
            .ToList();

        var rows = entryRows.Concat(movementRows)
            .OrderBy(r => r.OccurredAt, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("OccurredAt,TabId,TabName,TabType,Kind,Amount,Note");
        foreach (var r in rows)
        {
            sb.Append(CsvField(r.OccurredAt)).Append(',')
                .Append(r.TabId.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(CsvField(r.TabName)).Append(',')
                .Append(CsvField(r.TabType)).Append(',')
                .Append(CsvField(r.Kind)).Append(',')
                .Append(r.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(CsvField(r.Note))
                .AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private byte[] BuildStockSnapshotCsv()
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<StockSnapshotRow>(
            $"""
            SELECT
              i.Id        AS ItemId,
              i.Name      AS Name,
              i.Sku       AS Sku,
              {StockSnapshotQuery.CategorySelectExpr} AS Category,
              i.ItemType  AS ItemType,
              i.StockQty  AS StockQty,
              i.IsActive  AS IsActive
            FROM Items i
            {StockSnapshotQuery.ReportWhereClause}
            {StockSnapshotQuery.OrderByClause}
            """)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("ItemId,Name,Sku,Category,ItemType,StockQty,IsActive");
        foreach (var r in rows)
        {
            sb.Append(r.ItemId.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(CsvField(r.Name)).Append(',')
                .Append(CsvField(r.Sku)).Append(',')
                .Append(CsvField(r.Category)).Append(',')
                .Append(CsvField(r.ItemType)).Append(',')
                .Append(r.StockQty.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(r.IsActive != 0 ? "1" : "0")
                .AppendLine();
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private IReadOnlyList<ArchivedTabListRow> ListArchivedTabs()
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<ArchivedRow>(
            """
            SELECT
              Id,
              COALESCE(DisplayName, Name) AS DisplayName,
              COALESCE(TabType, '')        AS TabType,
              COALESCE(Balance, 0)         AS Balance,
              LastActivityAt,
              ClosedAt,
              COALESCE(IsClosed, 0)        AS IsClosed,
              COALESCE(IsArchived, 0)      AS IsArchived
            FROM Tabs
            WHERE COALESCE(IsArchived, 0) = 1 OR COALESCE(IsClosed, 0) = 1
            ORDER BY COALESCE(ClosedAt, LastActivityAt, CreatedAt) DESC
            """)
            .ToList();

        var list = new List<ArchivedTabListRow>(rows.Count);
        foreach (var r in rows)
        {
            list.Add(new ArchivedTabListRow(
                r.Id,
                string.IsNullOrWhiteSpace(r.DisplayName) ? $"Tab {r.Id}" : r.DisplayName,
                string.IsNullOrWhiteSpace(r.TabType) ? "Unknown" : r.TabType,
                r.Balance,
                ParseDate(r.LastActivityAt),
                ParseDate(r.ClosedAt),
                r.IsClosed != 0,
                r.IsArchived != 0));
        }

        return list;
    }

    private static DateTime? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt.ToLocalTime();
        }

        return null;
    }

    private static string CsvField(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var needsQuotes = raw.Contains(',') || raw.Contains('"') || raw.Contains('\n') || raw.Contains('\r');
        var escaped = raw.Replace("\"", "\"\"");
        return needsQuotes ? "\"" + escaped + "\"" : escaped;
    }

    private sealed class MonthlyEntryRow
    {
        public long TabId { get; set; }

        public string? TabName { get; set; }

        public string? TabType { get; set; }

        public string? Kind { get; set; }

        public double? Amount { get; set; }

        public string? Note { get; set; }

        public string? OccurredAt { get; set; }
    }

    private sealed class StockSnapshotRow
    {
        public long ItemId { get; set; }

        public string? Name { get; set; }

        public string? Sku { get; set; }

        public string? Category { get; set; }

        public string? ItemType { get; set; }

        public long StockQty { get; set; }

        public long IsActive { get; set; }
    }

    private sealed class ArchivedRow
    {
        public long Id { get; set; }

        public string? DisplayName { get; set; }

        public string? TabType { get; set; }

        public double Balance { get; set; }

        public string? LastActivityAt { get; set; }

        public string? ClosedAt { get; set; }

        public long IsClosed { get; set; }

        public long IsArchived { get; set; }
    }
}
