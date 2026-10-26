using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Payments;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteSquarePaymentAttemptRepository : ISquarePaymentAttemptRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteSquarePaymentAttemptRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<SquarePaymentAttemptBeginResult> BeginAsync(
        SquarePaymentAttemptBeginRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = request.IdempotencyKey.Trim();
        if (key.Length == 0)
        {
            throw new ArgumentException("Idempotency key is required.", nameof(request));
        }

        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        var existing = conn.QueryFirstOrDefault<SquareAttemptRow>(
            """
            SELECT Id, Status, SquarePaymentId, SquareCheckoutId, PaymentType, TabLegacyId,
                   BaseAmount, SurchargeAmount, ChargedAmount, RecoveryStatus
            FROM SquarePaymentAttempts
            WHERE IdempotencyKey = @key
            LIMIT 1
            """,
            new { key }, tx);

        if (existing is not null)
        {
            if (!string.Equals(existing.PaymentType, request.PaymentType.ToString(), StringComparison.Ordinal)
                || !string.Equals(existing.TabLegacyId ?? string.Empty, request.TabLegacyId?.Trim() ?? string.Empty, StringComparison.Ordinal)
                || existing.BaseAmount != request.BaseAmount
                || existing.SurchargeAmount != request.SurchargeAmount
                || existing.ChargedAmount != request.ChargedAmount)
            {
                tx.Commit();
                return Task.FromResult(new SquarePaymentAttemptBeginResult
                {
                    AttemptId = existing.Id,
                    IdempotencyKey = key,
                    RequiresRecovery = true,
                    SquarePaymentId = existing.SquarePaymentId,
                    SquareCheckoutId = existing.SquareCheckoutId,
                });
            }
            var status = existing.Status ?? string.Empty;
            var completed = string.Equals(status, nameof(SquarePaymentAttemptStatus.Completed), StringComparison.OrdinalIgnoreCase);
            var resolved = existing.RecoveryStatus is SquareRecoveryStatuses.ManuallyReconciled
                or SquareRecoveryStatuses.LinkedPitstop or SquareRecoveryStatuses.LinkedTab or SquareRecoveryStatuses.Ignored;
            var terminalApproved = !resolved && string.Equals(status, nameof(SquarePaymentAttemptStatus.TerminalApproved), StringComparison.OrdinalIgnoreCase);
            var failed = resolved || status is nameof(SquarePaymentAttemptStatus.Failed) or nameof(SquarePaymentAttemptStatus.Cancelled);
            tx.Commit();
            return Task.FromResult(new SquarePaymentAttemptBeginResult
            {
                AttemptId = existing.Id,
                IdempotencyKey = key,
                AlreadyCompleted = completed,
                AlreadyTerminalApproved = terminalApproved,
                AlreadyFailed = failed,
                RequiresRecovery = !completed && !terminalApproved && !failed,
                SquarePaymentId = existing.SquarePaymentId,
                SquareCheckoutId = existing.SquareCheckoutId,
            });
        }

        // Also protect a new UI session after a restart or an edited amount. An approved
        // payment without a local record must be reconciled before collecting again.
        var unresolved = conn.QueryFirstOrDefault<SquareAttemptRow>(
            """
            SELECT Id, IdempotencyKey, SquarePaymentId, SquareCheckoutId
            FROM SquarePaymentAttempts
            WHERE PaymentType = @paymentType
              AND COALESCE(TabLegacyId, '') = @tabLegacyId
              AND Status IN ('Pending', 'Unknown', 'TimedOut', 'TerminalApproved', 'Approved')
              AND LocalPaymentId IS NULL AND PitstopSaleId IS NULL
              AND UPPER(TRIM(COALESCE(RecoveryStatus, ''))) NOT IN ('MANUALLYRECONCILED', 'LINKEDPITSTOP', 'LINKEDTAB', 'IGNORED')
            ORDER BY Id LIMIT 1
            """,
            new { paymentType = request.PaymentType.ToString(), tabLegacyId = request.TabLegacyId?.Trim() ?? string.Empty }, tx);
        if (unresolved is not null)
        {
            tx.Commit();
            return Task.FromResult(new SquarePaymentAttemptBeginResult
            {
                AttemptId = unresolved.Id,
                IdempotencyKey = unresolved.IdempotencyKey,
                RequiresRecovery = true,
                SquarePaymentId = unresolved.SquarePaymentId,
                SquareCheckoutId = unresolved.SquareCheckoutId,
            });
        }

        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var attemptId = conn.ExecuteScalar<long>(
            """
            INSERT INTO SquarePaymentAttempts (
              IdempotencyKey, Status, PaymentType, TabLegacyId,
              BaseAmount, SurchargeAmount, ChargedAmount, CreatedAt, UpdatedAt)
            VALUES (
              @key, @status, @paymentType, @tabLegacyId,
              @baseAmount, @surchargeAmount, @chargedAmount, @now, @now);
            SELECT last_insert_rowid();
            """,
            new
            {
                key,
                status = nameof(SquarePaymentAttemptStatus.Pending),
                paymentType = request.PaymentType.ToString(),
                tabLegacyId = string.IsNullOrWhiteSpace(request.TabLegacyId) ? null : request.TabLegacyId.Trim(),
                baseAmount = request.BaseAmount,
                surchargeAmount = request.SurchargeAmount,
                chargedAmount = request.ChargedAmount,
                now,
            }, tx);

        tx.Commit();

        return Task.FromResult(new SquarePaymentAttemptBeginResult
        {
            AttemptId = attemptId,
            IdempotencyKey = key,
        });
    }

    public Task MarkTerminalApprovedAsync(
        long attemptId,
        string squarePaymentId,
        string? squareCheckoutId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var conn = _factory.OpenConnection();
        conn.Execute(
            """
            UPDATE SquarePaymentAttempts
            SET Status = @status,
                SquarePaymentId = @paymentId,
                SquareCheckoutId = COALESCE(@checkoutId, SquareCheckoutId),
                UpdatedAt = @now
            WHERE Id = @id AND Status != 'Completed'
              AND UPPER(TRIM(COALESCE(RecoveryStatus, ''))) NOT IN ('MANUALLYRECONCILED', 'LINKEDPITSTOP', 'LINKEDTAB', 'IGNORED')
            """,
            new
            {
                id = attemptId,
                status = nameof(SquarePaymentAttemptStatus.TerminalApproved),
                paymentId = squarePaymentId.Trim(),
                checkoutId = string.IsNullOrWhiteSpace(squareCheckoutId) ? null : squareCheckoutId.Trim(),
                now,
            });

        return Task.CompletedTask;
    }

    public Task SaveCheckoutIdAsync(long attemptId, string checkoutId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(new CommandDefinition(
            "UPDATE SquarePaymentAttempts SET SquareCheckoutId = @checkoutId, UpdatedAt = @now WHERE Id = @attemptId",
            new { attemptId, checkoutId, now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
            cancellationToken: cancellationToken));
        return Task.CompletedTask;
    }

    public Task MarkUncertainAsync(long attemptId, string reason, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(new CommandDefinition(
            """
            UPDATE SquarePaymentAttempts
            SET Status = 'Unknown', FailureReason = @reason, RecoveryStatus = @recoveryStatus, UpdatedAt = @now
            WHERE Id = @attemptId AND Status NOT IN ('Completed', 'TerminalApproved')
            """,
            new { attemptId, reason, recoveryStatus = SquareRecoveryStatuses.PendingReview, now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
            cancellationToken: cancellationToken));
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(long attemptId, string reason, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var conn = _factory.OpenConnection();
        conn.Execute(
            """
            UPDATE SquarePaymentAttempts
            SET Status = @status,
                FailureReason = @reason,
                UpdatedAt = @now
            WHERE Id = @id AND Status NOT IN ('Completed', 'TerminalApproved')
            """,
            new
            {
                id = attemptId,
                status = nameof(SquarePaymentAttemptStatus.Failed),
                reason = reason.Trim(),
                now,
            });

        return Task.CompletedTask;
    }

    public Task<bool> MarkCompletedAsync(
        long attemptId,
        string squarePaymentId,
        long? localPaymentId,
        long? pitstopSaleId,
        string? pitstopSaleGuid,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var conn = _factory.OpenConnection();
        var affected = conn.Execute(
            new CommandDefinition(
                """
                UPDATE SquarePaymentAttempts
                SET Status = @status,
                    SquarePaymentId = @paymentId,
                    LocalPaymentId = @localPaymentId,
                    PitstopSaleId = @pitstopSaleId,
                    PitstopSaleGuid = @pitstopSaleGuid,
                    UpdatedAt = @now
                WHERE Id = @id
                """,
                new
                {
                    id = attemptId,
                    status = nameof(SquarePaymentAttemptStatus.Completed),
                    paymentId = squarePaymentId.Trim(),
                    localPaymentId,
                    pitstopSaleId,
                    pitstopSaleGuid = string.IsNullOrWhiteSpace(pitstopSaleGuid) ? null : pitstopSaleGuid.Trim(),
                    now,
                },
                cancellationToken: cancellationToken));

        return Task.FromResult(affected > 0);
    }

    public Task SaveRecoveryPayloadAsync(
        long attemptId,
        string recoveryPayloadJson,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        using var conn = _factory.OpenConnection();
        conn.Execute(
            new CommandDefinition(
                """
                UPDATE SquarePaymentAttempts
                SET RecoveryPayloadJson = @payload,
                    RecoveryStatus = @recoveryStatus,
                    UpdatedAt = @now
                WHERE Id = @id
                """,
                new
                {
                    id = attemptId,
                    payload = recoveryPayloadJson,
                    recoveryStatus = SquareRecoveryStatuses.PendingReview,
                    now,
                },
                cancellationToken: cancellationToken));

        return Task.CompletedTask;
    }

    public Task<System.Collections.Generic.List<string>> GetBarTabSquarePaymentIdsAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var paymentIds = conn.Query<string>(
            new CommandDefinition(
                """
                SELECT SquarePaymentId
                FROM SquarePaymentAttempts
                WHERE PaymentType = 'TabTopUp'
                  AND SquarePaymentId IS NOT NULL
                  AND CreatedAt >= @start
                  AND CreatedAt < @end
                """,
                new
                {
                    start = periodStart.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                    end = periodEnd.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                },
                cancellationToken: cancellationToken));

        return Task.FromResult(paymentIds.ToList());
    }

    private sealed class SquareAttemptRow
    {
        public long Id { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? SquarePaymentId { get; init; }

        public string? SquareCheckoutId { get; init; }

        public string IdempotencyKey { get; init; } = string.Empty;
        public string? PaymentType { get; init; }
        public string? TabLegacyId { get; init; }
        public decimal BaseAmount { get; init; }
        public decimal SurchargeAmount { get; init; }
        public decimal ChargedAmount { get; init; }
        public string? RecoveryStatus { get; init; }
    }
}
