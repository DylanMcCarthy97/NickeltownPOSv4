using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Payments;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteSquareRecoveryRepository : ISquareRecoveryRepository
{
    private const string OrphanWhereClause = """
        UPPER(TRIM(COALESCE(spa.Status,''))) IN ('TERMINALAPPROVED','COMPLETED','APPROVED')
        AND (spa.PitstopSaleId IS NULL)
        AND (spa.LocalPaymentId IS NULL)
        AND (UPPER(TRIM(COALESCE(spa.RecoveryStatus,''))) NOT IN ('MANUALLYRECONCILED','LINKEDPITSTOP','LINKEDTAB','IGNORED'))
        """;

    private readonly SqliteConnectionFactory _factory;

    public SqliteSquareRecoveryRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<int> GetUnresolvedCountAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                $"""
                SELECT COUNT(1) FROM SquarePaymentAttempts spa
                WHERE {OrphanWhereClause}
                """,
                cancellationToken: cancellationToken));
        return Task.FromResult(count);
    }

    public Task<IReadOnlyList<SquareRecoveryRow>> GetOrphanAttemptsAsync(
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<SquareRecoveryDbRow>(
            new CommandDefinition(
                $"""
                SELECT
                  spa.Id AS AttemptId,
                  COALESCE(spa.UpdatedAt, spa.CreatedAt) AS OccurredAt,
                  COALESCE(spa.Status, '') AS Status,
                  COALESCE(spa.PaymentType, '') AS PaymentType,
                  spa.TabLegacyId,
                  spa.BaseAmount,
                  spa.SurchargeAmount,
                  spa.ChargedAmount,
                  spa.SquarePaymentId,
                  spa.SquareCheckoutId,
                  spa.IdempotencyKey,
                  spa.InitiatedByStaffName,
                  spa.FailureReason,
                  spa.RecoveryStatus,
                  spa.RecoveryNote,
                  spa.RecoveryPayloadJson
                FROM SquarePaymentAttempts spa
                WHERE {OrphanWhereClause}
                ORDER BY datetime(COALESCE(spa.UpdatedAt, spa.CreatedAt)) DESC, spa.Id DESC
                """,
                cancellationToken: cancellationToken));

        var list = rows.Select(MapRow).ToList();
        return Task.FromResult<IReadOnlyList<SquareRecoveryRow>>(list);
    }

    public Task<SquareRecoveryUpdateResult> MarkManuallyReconciledAsync(
        long attemptId,
        string? note,
        CancellationToken cancellationToken = default) =>
        SetRecoveryStatusAsync(attemptId, SquareRecoveryStatuses.ManuallyReconciled, note, cancellationToken);

    public Task<SquareRecoveryUpdateResult> LinkPitstopSaleAsync(
        long attemptId,
        long pitstopSaleId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var saleGuid = conn.ExecuteScalar<string?>(
                new CommandDefinition(
                    "SELECT SaleGuid FROM PitstopSales WHERE Id = @id LIMIT 1",
                    new { id = pitstopSaleId },
                    tx,
                    cancellationToken: cancellationToken));

            if (string.IsNullOrWhiteSpace(saleGuid))
            {
                tx.Rollback();
                return Task.FromResult(SquareRecoveryUpdateResult.Fail("Pitstop sale not found."));
            }

            var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE SquarePaymentAttempts
                    SET PitstopSaleId = @saleId,
                        PitstopSaleGuid = @saleGuid,
                        RecoveryStatus = @recoveryStatus,
                        RecoveryNote = TRIM(COALESCE(@note, RecoveryNote)),
                        UpdatedAt = @now
                    WHERE Id = @attemptId
                    """,
                    new
                    {
                        attemptId,
                        saleId = pitstopSaleId,
                        saleGuid,
                        recoveryStatus = SquareRecoveryStatuses.LinkedPitstop,
                        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                        now,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            tx.Commit();
            return Task.FromResult(SquareRecoveryUpdateResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(SquareRecoveryUpdateResult.Fail(ex.Message));
        }
    }

    public Task<SquareRecoveryUpdateResult> LinkTabPaymentAsync(
        long attemptId,
        long localPaymentId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var paymentExists = conn.ExecuteScalar<long>(
                new CommandDefinition(
                    "SELECT COUNT(1) FROM Payments WHERE Id = @id",
                    new { id = localPaymentId },
                    tx,
                    cancellationToken: cancellationToken));

            if (paymentExists == 0)
            {
                tx.Rollback();
                return Task.FromResult(SquareRecoveryUpdateResult.Fail("Tab payment row not found."));
            }

            var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE SquarePaymentAttempts
                    SET LocalPaymentId = @paymentId,
                        RecoveryStatus = @recoveryStatus,
                        RecoveryNote = TRIM(COALESCE(@note, RecoveryNote)),
                        UpdatedAt = @now
                    WHERE Id = @attemptId
                    """,
                    new
                    {
                        attemptId,
                        paymentId = localPaymentId,
                        recoveryStatus = SquareRecoveryStatuses.LinkedTab,
                        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                        now,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            tx.Commit();
            return Task.FromResult(SquareRecoveryUpdateResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(SquareRecoveryUpdateResult.Fail(ex.Message));
        }
    }

    public Task<PaymentRecoveryAlertSummary?> GetPrimaryRecoveryAlertAsync(
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var row = conn.QueryFirstOrDefault<SquareRecoveryDbRow>(
            new CommandDefinition(
                $"""
                SELECT
                  spa.Id AS AttemptId,
                  COALESCE(spa.UpdatedAt, spa.CreatedAt) AS OccurredAt,
                  COALESCE(spa.Status, '') AS Status,
                  COALESCE(spa.PaymentType, '') AS PaymentType,
                  spa.TabLegacyId,
                  spa.BaseAmount,
                  spa.SurchargeAmount,
                  spa.ChargedAmount,
                  spa.SquarePaymentId,
                  spa.SquareCheckoutId,
                  spa.IdempotencyKey,
                  spa.InitiatedByStaffName,
                  spa.FailureReason,
                  spa.RecoveryStatus,
                  spa.RecoveryNote,
                  spa.RecoveryPayloadJson
                FROM SquarePaymentAttempts spa
                WHERE {OrphanWhereClause}
                ORDER BY datetime(COALESCE(spa.UpdatedAt, spa.CreatedAt)) DESC, spa.Id DESC
                LIMIT 1
                """,
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return Task.FromResult<PaymentRecoveryAlertSummary?>(null);
        }

        var mapped = MapRow(row);
        var txnId = !string.IsNullOrWhiteSpace(mapped.SquarePaymentId)
            ? mapped.SquarePaymentId!
            : mapped.IdempotencyKey ?? mapped.AttemptId.ToString(CultureInfo.InvariantCulture);

        return Task.FromResult<PaymentRecoveryAlertSummary?>(
            new PaymentRecoveryAlertSummary
            {
                AttemptId = mapped.AttemptId,
                ChargedAmount = mapped.ChargedAmount,
                OccurredAt = mapped.OccurredAt,
                TransactionId = txnId,
                PaymentType = mapped.PaymentType,
            });
    }

    public Task<SquareRecoveryUpdateResult> MarkIgnoredAsync(
        long attemptId,
        string? note,
        CancellationToken cancellationToken = default) =>
        SetRecoveryStatusAsync(attemptId, SquareRecoveryStatuses.Ignored, note, cancellationToken);

    public Task<string?> GetRecoveryPayloadJsonAsync(
        long attemptId,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var json = conn.ExecuteScalar<string?>(
            new CommandDefinition(
                "SELECT RecoveryPayloadJson FROM SquarePaymentAttempts WHERE Id = @id LIMIT 1",
                new { id = attemptId },
                cancellationToken: cancellationToken));

        return Task.FromResult(string.IsNullOrWhiteSpace(json) ? null : json);
    }

    public Task<SquareRecoveryUpdateResult> AddNoteAsync(
        long attemptId,
        string note,
        CancellationToken cancellationToken = default)
    {
        var trimmed = (note ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return Task.FromResult(SquareRecoveryUpdateResult.Fail("Note is empty."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE SquarePaymentAttempts
                    SET RecoveryNote = CASE
                          WHEN RecoveryNote IS NULL OR trim(RecoveryNote) = '' THEN @note
                          ELSE RecoveryNote || char(10) || @note
                        END,
                        UpdatedAt = @now
                    WHERE Id = @attemptId
                    """,
                    new { attemptId, note = trimmed, now },
                    cancellationToken: cancellationToken));

            return Task.FromResult(SquareRecoveryUpdateResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(SquareRecoveryUpdateResult.Fail(ex.Message));
        }
    }

    private Task<SquareRecoveryUpdateResult> SetRecoveryStatusAsync(
        long attemptId,
        string status,
        string? note,
        CancellationToken cancellationToken)
    {
        try
        {
            using var conn = _factory.OpenConnection();
            var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE SquarePaymentAttempts
                    SET RecoveryStatus = @status,
                        RecoveryNote = COALESCE(@note, RecoveryNote),
                        UpdatedAt = @now
                    WHERE Id = @attemptId
                    """,
                    new
                    {
                        attemptId,
                        status,
                        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                        now,
                    },
                    cancellationToken: cancellationToken));

            return Task.FromResult(SquareRecoveryUpdateResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(SquareRecoveryUpdateResult.Fail(ex.Message));
        }
    }

    private static SquareRecoveryRow MapRow(SquareRecoveryDbRow r) =>
        new()
        {
            AttemptId = r.AttemptId,
            OccurredAt = ParseOffset(r.OccurredAt),
            Status = r.Status,
            PaymentType = r.PaymentType,
            TabLegacyId = r.TabLegacyId,
            BaseAmount = decimal.Round(r.BaseAmount, 2, MidpointRounding.AwayFromZero),
            SurchargeAmount = decimal.Round(r.SurchargeAmount, 2, MidpointRounding.AwayFromZero),
            ChargedAmount = decimal.Round(r.ChargedAmount, 2, MidpointRounding.AwayFromZero),
            SquarePaymentId = r.SquarePaymentId,
            SquareCheckoutId = r.SquareCheckoutId,
            IdempotencyKey = r.IdempotencyKey,
            InitiatedByStaffName = r.InitiatedByStaffName,
            FailureReason = r.FailureReason,
            RecoveryStatus = r.RecoveryStatus,
            RecoveryNote = r.RecoveryNote,
            HasRecoverablePayload = !string.IsNullOrWhiteSpace(r.RecoveryPayloadJson),
        };

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

    private sealed class SquareRecoveryDbRow
    {
        public long AttemptId { get; init; }

        public string OccurredAt { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string PaymentType { get; init; } = string.Empty;

        public string? TabLegacyId { get; init; }

        public decimal BaseAmount { get; init; }

        public decimal SurchargeAmount { get; init; }

        public decimal ChargedAmount { get; init; }

        public string? SquarePaymentId { get; init; }

        public string? SquareCheckoutId { get; init; }

        public string? IdempotencyKey { get; init; }

        public string? InitiatedByStaffName { get; init; }

        public string? FailureReason { get; init; }

        public string? RecoveryStatus { get; init; }

        public string? RecoveryNote { get; init; }

        public string? RecoveryPayloadJson { get; init; }
    }
}
