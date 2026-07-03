using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqlitePitstopEodBatchRepository : IPitstopEodBatchRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly SqliteConnectionFactory _factory;

    public SqlitePitstopEodBatchRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<int> GetActivePitstopSaleCountAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND PitstopEodBatchId IS NULL
                  AND COALESCE(Status,'Active') = 'Active'
                """,
                cancellationToken: cancellationToken));
        return Task.FromResult(count);
    }

    public Task<int> GetActivePitstopSaleCountForPeriodAsync(
        DateTimeOffset periodStartLocal,
        DateTimeOffset periodEndLocal,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var periodStartIso = periodStartLocal.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var periodEndIso = periodEndLocal.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND PitstopEodBatchId IS NULL
                  AND COALESCE(Status,'Active') = 'Active'
                  AND datetime(COALESCE(SoldAt, CreatedAt)) >= datetime(@periodStartIso)
                  AND datetime(COALESCE(SoldAt, CreatedAt)) < datetime(@periodEndIso)
                """,
                new { periodStartIso, periodEndIso },
                cancellationToken: cancellationToken));
        return Task.FromResult(count);
    }

    public Task<int> GetNonPitstopSaleModeCountAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM PitstopSales
                WHERE trim(COALESCE(SaleMode, '')) <> ''
                  AND lower(trim(SaleMode)) <> 'pitstop'
                """,
                cancellationToken: cancellationToken));
        return Task.FromResult(count);
    }

    public Task<PitstopEodArchiveResult> ArchiveActivePitstopSalesAsync(
        PitstopEodArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var periodStart = request.PeriodStartLocal.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
            var periodEnd = request.PeriodEndLocal.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

            var activeCount = conn.ExecuteScalar<int>(
                new CommandDefinition(
                    """
                    SELECT COUNT(1) FROM PitstopSales
                    WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                      AND PitstopEodBatchId IS NULL
                      AND COALESCE(Status,'Active') = 'Active'
                      AND datetime(COALESCE(SoldAt, CreatedAt)) >= datetime(@periodStart)
                      AND datetime(COALESCE(SoldAt, CreatedAt)) < datetime(@periodEnd)
                    """,
                    new { periodStart, periodEnd },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (activeCount == 0)
            {
                tx.Rollback();
                return Task.FromResult(
                    PitstopEodArchiveResult.Fail(
                        "There are no active Pitstop sales in this EOD period to archive. This period may already be archived."));
            }

            var archivedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var reportJson = request.ReportData is null
                ? null
                : JsonSerializer.Serialize(request.ReportData, JsonOptions);
            var warningsJson = request.ReconciliationWarnings is { Count: > 0 }
                ? JsonSerializer.Serialize(request.ReconciliationWarnings, JsonOptions)
                : null;

            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO PitstopEodBatches (
                      ArchivedAt, OperatorName, OperatorStaffId, EventName,
                      PeriodStartLocal, PeriodEndLocal,
                      TotalSales, CashTotal, CardChargedTotal, CardBaseProductTotal,
                      CardSurchargeTotal, EstimatedSquareFees, NetTotal, SaleCount,
                      PdfPath, ReportDataJson, ReconciliationWarningsJson, CreatedAt,
                      Notes, StartingFloat, CashCounted, FloatRemoved, ExpectedCash, CashVariance,
                      BackupBeforePath, BackupAfterPath)
                    VALUES (
                      @ArchivedAt, @OperatorName, @OperatorStaffId, @EventName,
                      @PeriodStartLocal, @PeriodEndLocal,
                      @TotalSales, @CashTotal, @CardChargedTotal, @CardBaseProductTotal,
                      @CardSurchargeTotal, @EstimatedSquareFees, @NetTotal, @SaleCount,
                      @PdfPath, @ReportDataJson, @ReconciliationWarningsJson, datetime('now'),
                      @Notes, @StartingFloat, @CashCounted, @FloatRemoved, @ExpectedCash, @CashVariance,
                      @BackupBeforePath, @BackupAfterPath)
                    """,
                    new
                    {
                        ArchivedAt = archivedAt,
                        OperatorName = string.IsNullOrWhiteSpace(request.OperatorName) ? null : request.OperatorName.Trim(),
                        request.OperatorStaffId,
                        EventName = string.IsNullOrWhiteSpace(request.EventName) ? null : request.EventName.Trim(),
                        PeriodStartLocal = periodStart,
                        PeriodEndLocal = periodEnd,
                        request.TotalSales,
                        request.CashTotal,
                        request.CardChargedTotal,
                        request.CardBaseProductTotal,
                        request.CardSurchargeTotal,
                        request.EstimatedSquareFees,
                        request.NetTotal,
                        SaleCount = activeCount,
                        PdfPath = string.IsNullOrWhiteSpace(request.PdfPath) ? null : request.PdfPath.Trim(),
                        ReportDataJson = reportJson,
                        ReconciliationWarningsJson = warningsJson,
                        Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                        request.StartingFloat,
                        request.CashCounted,
                        request.FloatRemoved,
                        request.ExpectedCash,
                        request.CashVariance,
                        BackupBeforePath = string.IsNullOrWhiteSpace(request.BackupBeforePath) ? null : request.BackupBeforePath.Trim(),
                        BackupAfterPath = string.IsNullOrWhiteSpace(request.BackupAfterPath) ? null : request.BackupAfterPath.Trim(),
                    },
                    tx,
                    cancellationToken: cancellationToken));

            var batchId = conn.QuerySingle<long>(
                new CommandDefinition("SELECT last_insert_rowid()", transaction: tx, cancellationToken: cancellationToken));

            var updated = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE PitstopSales
                    SET PitstopEodBatchId = @batchId,
                        Status = 'Archived'
                    WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                      AND PitstopEodBatchId IS NULL
                      AND COALESCE(Status,'Active') = 'Active'
                      AND datetime(COALESCE(SoldAt, CreatedAt)) >= datetime(@periodStart)
                      AND datetime(COALESCE(SoldAt, CreatedAt)) < datetime(@periodEnd)
                    """,
                    new { batchId, periodStart, periodEnd },
                    tx,
                    cancellationToken: cancellationToken));

            if (updated != activeCount)
            {
                tx.Rollback();
                return Task.FromResult(
                    PitstopEodArchiveResult.Fail("Archive conflict — active Pitstop sales changed during archive. Try again."));
            }

            tx.Commit();
            return Task.FromResult(PitstopEodArchiveResult.Success(batchId, activeCount));
        }
        catch (Exception ex)
        {
            return Task.FromResult(PitstopEodArchiveResult.Fail(ex.Message));
        }
    }

    public Task<IReadOnlyList<PitstopEodBatchListRow>> ListBatchesAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<PitstopEodBatchDbRow>(
            new CommandDefinition(
                """
                SELECT
                  Id, ArchivedAt, EventName, TotalSales, CashTotal, CardChargedTotal,
                  CardSurchargeTotal, EstimatedSquareFees, NetTotal, SaleCount, OperatorName, PdfPath
                FROM PitstopEodBatches
                ORDER BY datetime(ArchivedAt) DESC
                """,
                cancellationToken: cancellationToken));

        var list = rows.Select(MapListRow).ToList();
        return Task.FromResult<IReadOnlyList<PitstopEodBatchListRow>>(list);
    }

    public Task<PitstopEodBatchDetail?> GetBatchDetailAsync(long batchId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var row = conn.QuerySingleOrDefault<PitstopEodBatchDbRow>(
            new CommandDefinition(
                """
                SELECT
                  Id, ArchivedAt, OperatorName, OperatorStaffId, EventName,
                  PeriodStartLocal, PeriodEndLocal,
                  TotalSales, CashTotal, CardChargedTotal, CardBaseProductTotal,
                  CardSurchargeTotal, EstimatedSquareFees, NetTotal, SaleCount,
                  PdfPath, ReportDataJson, ReconciliationWarningsJson,
                  Notes, StartingFloat, CashCounted, FloatRemoved, ExpectedCash, CashVariance,
                  BackupBeforePath, BackupAfterPath
                FROM PitstopEodBatches
                WHERE Id = @batchId
                """,
                new { batchId },
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return Task.FromResult<PitstopEodBatchDetail?>(null);
        }

        return Task.FromResult<PitstopEodBatchDetail?>(MapDetailRow(row));
    }

    public Task<bool> AppendNoteAsync(long batchId, string note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return Task.FromResult(false);
        }

        var trimmed = note.Trim();
        var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var entry = $"[{stamp}] {trimmed}";

        using var conn = _factory.OpenConnection();
        var affected = conn.Execute(
            new CommandDefinition(
                """
                UPDATE PitstopEodBatches
                SET Notes = CASE
                              WHEN Notes IS NULL OR TRIM(Notes) = '' THEN @entry
                              ELSE Notes || CHAR(10) || @entry
                            END
                WHERE Id = @batchId
                """,
                new { batchId, entry },
                cancellationToken: cancellationToken));

        return Task.FromResult(affected > 0);
    }

    public Task<bool> UpdateBackupAfterPathAsync(long batchId, string? backupAfterPath, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var trimmed = string.IsNullOrWhiteSpace(backupAfterPath) ? null : backupAfterPath.Trim();
        var affected = conn.Execute(
            new CommandDefinition(
                "UPDATE PitstopEodBatches SET BackupAfterPath = @backupAfterPath WHERE Id = @batchId",
                new { batchId, backupAfterPath = trimmed },
                cancellationToken: cancellationToken));

        return Task.FromResult(affected > 0);
    }

    public Task<IReadOnlyList<PitstopArchivedSaleRow>> GetBatchSalesAsync(
        long batchId,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<PitstopArchivedSaleDbRow>(
            new CommandDefinition(
                """
                SELECT
                  Id AS SaleId,
                  COALESCE(SoldAt, CreatedAt) AS SoldAt,
                  Total,
                  COALESCE(NULLIF(TRIM(PaymentMethod), ''), '—') AS PaymentMethod,
                  StaffDisplayName,
                  BaseProductTotal,
                  CardSurchargeAmount,
                  SquareExternalRef
                FROM PitstopSales
                WHERE PitstopEodBatchId = @batchId
                ORDER BY datetime(COALESCE(SoldAt, CreatedAt)), Id
                """,
                new { batchId },
                cancellationToken: cancellationToken));

        var list = rows.Select(r => new PitstopArchivedSaleRow
        {
            SaleId = r.SaleId,
            SoldAt = ParseOffset(r.SoldAt),
            Total = decimal.Round(r.Total, 2, MidpointRounding.AwayFromZero),
            PaymentMethod = r.PaymentMethod,
            StaffDisplayName = r.StaffDisplayName,
            BaseProductTotal = r.BaseProductTotal,
            CardSurchargeAmount = r.CardSurchargeAmount,
            SquareExternalRef = r.SquareExternalRef,
        }).ToList();

        return Task.FromResult<IReadOnlyList<PitstopArchivedSaleRow>>(list);
    }

    public Task<IReadOnlyList<PitstopSaleLineReportRow>> GetBatchItemisedLinesAsync(
        long batchId,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<PitstopSaleLineReportRow>(
            new CommandDefinition(
                """
                SELECT
                  COALESCE(li.ItemId, 0) AS ItemId,
                  COALESCE(NULLIF(TRIM(li.ItemName), ''), '(Unknown)') AS ItemName,
                  COALESCE(NULLIF(TRIM(c.Name), ''), 'General') AS CategoryName,
                  COALESCE(NULLIF(TRIM(ps.PaymentMethod), ''), '—') AS PaymentMethod,
                  li.Quantity AS Quantity,
                  COALESCE(li.UnitPrice, CASE WHEN li.Quantity > 0 THEN li.LineTotal / li.Quantity ELSE 0 END) AS UnitPrice,
                  COALESCE(li.LineTotal, 0) AS LineTotal
                FROM PitstopSaleItems li
                INNER JOIN PitstopSales ps ON ps.Id = li.PitstopSaleId
                LEFT JOIN Items i ON i.Id = li.ItemId
                LEFT JOIN Categories c ON c.Id = i.CategoryId
                WHERE ps.PitstopEodBatchId = @batchId
                ORDER BY datetime(COALESCE(ps.SoldAt, ps.CreatedAt)), li.Id
                """,
                new { batchId },
                cancellationToken: cancellationToken));

        return Task.FromResult<IReadOnlyList<PitstopSaleLineReportRow>>(rows.AsList());
    }

    private static PitstopEodBatchListRow MapListRow(PitstopEodBatchDbRow row) =>
        new()
        {
            Id = row.Id,
            ArchivedAt = ParseOffset(row.ArchivedAt),
            EventName = row.EventName,
            TotalSales = decimal.Round(row.TotalSales, 2, MidpointRounding.AwayFromZero),
            CashTotal = decimal.Round(row.CashTotal, 2, MidpointRounding.AwayFromZero),
            CardChargedTotal = decimal.Round(row.CardChargedTotal, 2, MidpointRounding.AwayFromZero),
            CardSurchargeTotal = decimal.Round(row.CardSurchargeTotal, 2, MidpointRounding.AwayFromZero),
            EstimatedSquareFees = decimal.Round(row.EstimatedSquareFees, 2, MidpointRounding.AwayFromZero),
            NetTotal = decimal.Round(row.NetTotal, 2, MidpointRounding.AwayFromZero),
            SaleCount = row.SaleCount,
            OperatorName = row.OperatorName,
            PdfPath = row.PdfPath,
        };

    private static PitstopEodBatchDetail MapDetailRow(PitstopEodBatchDbRow row)
    {
        PitstopReportData? reportData = null;
        if (!string.IsNullOrWhiteSpace(row.ReportDataJson))
        {
            try
            {
                reportData = JsonSerializer.Deserialize<PitstopReportData>(row.ReportDataJson, JsonOptions);
            }
            catch
            {
                // snapshot unavailable
            }
        }

        IReadOnlyList<string> warnings = [];
        if (!string.IsNullOrWhiteSpace(row.ReconciliationWarningsJson))
        {
            try
            {
                warnings = JsonSerializer.Deserialize<List<string>>(row.ReconciliationWarningsJson, JsonOptions) ?? [];
            }
            catch
            {
                warnings = [];
            }
        }

        return new PitstopEodBatchDetail
        {
            Id = row.Id,
            ArchivedAt = ParseOffset(row.ArchivedAt),
            OperatorName = row.OperatorName,
            OperatorStaffId = row.OperatorStaffId,
            EventName = row.EventName,
            PeriodStartLocal = string.IsNullOrWhiteSpace(row.PeriodStartLocal) ? null : ParseOffset(row.PeriodStartLocal),
            PeriodEndLocal = string.IsNullOrWhiteSpace(row.PeriodEndLocal) ? null : ParseOffset(row.PeriodEndLocal),
            TotalSales = decimal.Round(row.TotalSales, 2, MidpointRounding.AwayFromZero),
            CashTotal = decimal.Round(row.CashTotal, 2, MidpointRounding.AwayFromZero),
            CardChargedTotal = decimal.Round(row.CardChargedTotal, 2, MidpointRounding.AwayFromZero),
            CardBaseProductTotal = decimal.Round(row.CardBaseProductTotal, 2, MidpointRounding.AwayFromZero),
            CardSurchargeTotal = decimal.Round(row.CardSurchargeTotal, 2, MidpointRounding.AwayFromZero),
            EstimatedSquareFees = decimal.Round(row.EstimatedSquareFees, 2, MidpointRounding.AwayFromZero),
            NetTotal = decimal.Round(row.NetTotal, 2, MidpointRounding.AwayFromZero),
            SaleCount = row.SaleCount,
            PdfPath = row.PdfPath,
            ReportData = reportData,
            ReconciliationWarnings = warnings,
            Notes = row.Notes,
            StartingFloat = decimal.Round(row.StartingFloat, 2, MidpointRounding.AwayFromZero),
            CashCounted = row.CashCounted is null ? null : decimal.Round(row.CashCounted.Value, 2, MidpointRounding.AwayFromZero),
            FloatRemoved = row.FloatRemoved is null ? null : decimal.Round(row.FloatRemoved.Value, 2, MidpointRounding.AwayFromZero),
            ExpectedCash = row.ExpectedCash is null ? null : decimal.Round(row.ExpectedCash.Value, 2, MidpointRounding.AwayFromZero),
            CashVariance = row.CashVariance is null ? null : decimal.Round(row.CashVariance.Value, 2, MidpointRounding.AwayFromZero),
            BackupBeforePath = row.BackupBeforePath,
            BackupAfterPath = row.BackupAfterPath,
        };
    }

    private static DateTimeOffset ParseOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.MinValue;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto;
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    private sealed class PitstopArchivedSaleDbRow
    {
        public long SaleId { get; init; }

        public string SoldAt { get; init; } = string.Empty;

        public decimal Total { get; init; }

        public string PaymentMethod { get; init; } = string.Empty;

        public string? StaffDisplayName { get; init; }

        public decimal? BaseProductTotal { get; init; }

        public decimal? CardSurchargeAmount { get; init; }

        public string? SquareExternalRef { get; init; }
    }

    private sealed class PitstopEodBatchDbRow
    {
        public long Id { get; init; }

        public string ArchivedAt { get; init; } = string.Empty;

        public string? OperatorName { get; init; }

        public long? OperatorStaffId { get; init; }

        public string? EventName { get; init; }

        public string? PeriodStartLocal { get; init; }

        public string? PeriodEndLocal { get; init; }

        public decimal TotalSales { get; init; }

        public decimal CashTotal { get; init; }

        public decimal CardChargedTotal { get; init; }

        public decimal CardBaseProductTotal { get; init; }

        public decimal CardSurchargeTotal { get; init; }

        public decimal EstimatedSquareFees { get; init; }

        public decimal NetTotal { get; init; }

        public int SaleCount { get; init; }

        public string? PdfPath { get; init; }

        public string? ReportDataJson { get; init; }

        public string? ReconciliationWarningsJson { get; init; }

        public string? Notes { get; init; }

        public decimal StartingFloat { get; init; }

        public decimal? CashCounted { get; init; }

        public decimal? FloatRemoved { get; init; }

        public decimal? ExpectedCash { get; init; }

        public decimal? CashVariance { get; init; }

        public string? BackupBeforePath { get; init; }

        public string? BackupAfterPath { get; init; }
    }
}
