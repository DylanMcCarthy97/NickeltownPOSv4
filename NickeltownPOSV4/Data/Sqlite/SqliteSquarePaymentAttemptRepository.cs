using System;
using System.Globalization;
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
        var existing = conn.QueryFirstOrDefault<SquareAttemptRow>(
            """
            SELECT Id, Status, SquarePaymentId, SquareCheckoutId
            FROM SquarePaymentAttempts
            WHERE IdempotencyKey = @key
            LIMIT 1
            """,
            new { key });

        if (existing is not null)
        {
            var status = existing.Status ?? string.Empty;
            var completed = string.Equals(status, nameof(SquarePaymentAttemptStatus.Completed), StringComparison.OrdinalIgnoreCase);
            var terminalApproved = string.Equals(status, nameof(SquarePaymentAttemptStatus.TerminalApproved), StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(new SquarePaymentAttemptBeginResult
            {
                AttemptId = existing.Id,
                IdempotencyKey = key,
                AlreadyCompleted = completed,
                AlreadyTerminalApproved = terminalApproved,
                SquarePaymentId = existing.SquarePaymentId,
                SquareCheckoutId = existing.SquareCheckoutId,
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
            });

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
                SquareCheckoutId = @checkoutId,
                UpdatedAt = @now
            WHERE Id = @id
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
            WHERE Id = @id
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

    private sealed class SquareAttemptRow
    {
        public long Id { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? SquarePaymentId { get; init; }

        public string? SquareCheckoutId { get; init; }
    }
}