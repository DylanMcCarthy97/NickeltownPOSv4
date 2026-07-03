using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Payments;

namespace NickeltownPOSV4.Data.Sqlite;

public interface ISquareRecoveryRepository
{
    Task<int> GetUnresolvedCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SquareRecoveryRow>> GetOrphanAttemptsAsync(
        CancellationToken cancellationToken = default);

    Task<SquareRecoveryUpdateResult> MarkManuallyReconciledAsync(
        long attemptId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<SquareRecoveryUpdateResult> LinkPitstopSaleAsync(
        long attemptId,
        long pitstopSaleId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<SquareRecoveryUpdateResult> LinkTabPaymentAsync(
        long attemptId,
        long localPaymentId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<SquareRecoveryUpdateResult> AddNoteAsync(
        long attemptId,
        string note,
        CancellationToken cancellationToken = default);

    Task<PaymentRecoveryAlertSummary?> GetPrimaryRecoveryAlertAsync(
        CancellationToken cancellationToken = default);

    Task<SquareRecoveryUpdateResult> MarkIgnoredAsync(
        long attemptId,
        string? note,
        CancellationToken cancellationToken = default);

    Task<string?> GetRecoveryPayloadJsonAsync(
        long attemptId,
        CancellationToken cancellationToken = default);
}
