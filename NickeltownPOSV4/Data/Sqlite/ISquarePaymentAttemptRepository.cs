using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Payments;

namespace NickeltownPOSV4.Data.Sqlite;

public interface ISquarePaymentAttemptRepository
{
    Task<SquarePaymentAttemptBeginResult> BeginAsync(
        SquarePaymentAttemptBeginRequest request,
        CancellationToken cancellationToken = default);

    Task MarkTerminalApprovedAsync(
        long attemptId,
        string squarePaymentId,
        string? squareCheckoutId,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(long attemptId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the attempt as Completed and stores the local pointers.
    /// Implementations must not throw on missing rows; callers should check the return value
    /// and route any failure into the audit log / Square Recovery flow.
    /// </summary>
    Task<bool> MarkCompletedAsync(
        long attemptId,
        string squarePaymentId,
        long? localPaymentId,
        long? pitstopSaleId,
        string? pitstopSaleGuid,
        CancellationToken cancellationToken = default);

    Task SaveRecoveryPayloadAsync(
        long attemptId,
        string recoveryPayloadJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all Square payment IDs for bar tab top-ups within the specified period.
    /// Used to exclude bar tabs from Pitstop reports.
    /// </summary>
    Task<List<string>> GetBarTabSquarePaymentIdsAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);
}