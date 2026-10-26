using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Payments;

namespace NickeltownPOSV4.Services.Payments;

public interface ISquareCheckoutStatusReader
{
    Task<SquarePresentResult> CheckStatusAsync(string checkoutId, decimal chargedAmount, CancellationToken cancellationToken = default);
}

/// <summary>Refreshes existing checkouts and saves confirmed tab credits; never creates a charge.</summary>
public sealed class SquareRecoveryService
{
    private readonly ISquareRecoveryRepository _recovery;
    private readonly ISquarePaymentAttemptRepository _attempts;
    private readonly ISquareCheckoutStatusReader _reader;
    private readonly ITabFundsService _funds;

    public SquareRecoveryService(ISquareRecoveryRepository recovery, ISquarePaymentAttemptRepository attempts,
        ISquareCheckoutStatusReader reader, ITabFundsService funds)
    {
        _recovery = recovery; _attempts = attempts; _reader = reader; _funds = funds;
    }

    public async Task<SquareRecoveryUpdateResult> RefreshStatusAsync(long attemptId, CancellationToken cancellationToken = default)
    {
        var row = (await _recovery.GetOrphanAttemptsAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(r => r.AttemptId == attemptId);
        if (row is null) return SquareRecoveryUpdateResult.Fail("This payment is already resolved. Refresh the list.");
        if (string.IsNullOrWhiteSpace(row.SquareCheckoutId))
            return SquareRecoveryUpdateResult.Fail("No checkout ID was saved. Check this payment in the Square dashboard using its time, amount and transaction key.");
        var status = await _reader.CheckStatusAsync(row.SquareCheckoutId, row.ChargedAmount, cancellationToken).ConfigureAwait(false);
        if (status.Approved && !string.IsNullOrWhiteSpace(status.ExternalTransactionId))
        {
            await _attempts.MarkTerminalApprovedAsync(row.AttemptId, status.ExternalTransactionId, row.SquareCheckoutId, CancellationToken.None).ConfigureAwait(false);
            await _recovery.AddNoteAsync(row.AttemptId, "Square status check confirmed approval.", CancellationToken.None).ConfigureAwait(false);
            return SquareRecoveryUpdateResult.Success();
        }
        if (!status.RequiresRecovery && !status.Approved)
        {
            await _attempts.MarkFailedAsync(row.AttemptId, status.DeclineReason ?? "Square confirmed the payment did not complete.", CancellationToken.None).ConfigureAwait(false);
            await _recovery.AddNoteAsync(row.AttemptId, "Square confirmed cancellation or failure; no POS credit was added.", CancellationToken.None).ConfigureAwait(false);
            return SquareRecoveryUpdateResult.Success();
        }
        return SquareRecoveryUpdateResult.Fail(status.DeclineReason ?? "Square has not confirmed a final status. Keep this payment in recovery.");
    }

    public async Task<SquareRecoveryUpdateResult> SaveTabTopUpAsync(long attemptId, CancellationToken cancellationToken = default)
    {
        var row = (await _recovery.GetOrphanAttemptsAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(r => r.AttemptId == attemptId);
        if (row is null) return SquareRecoveryUpdateResult.Success(); // Already saved or reconciled.
        if (row.PaymentType != "TabTopUp" || row.Status is not ("TerminalApproved" or "Approved" or "Completed")
            || string.IsNullOrWhiteSpace(row.SquarePaymentId) || string.IsNullOrWhiteSpace(row.TabLegacyId))
            return SquareRecoveryUpdateResult.Fail("Confirm the tab payment is approved in Square before saving its top-up.");
        var saved = await _funds.CommitSquareCardTopUpAsync(row.TabLegacyId, row.BaseAmount, "Recovered approved Square top-up",
            new SquarePaymentCommitMetadata
            {
                IdempotencyKey = row.IdempotencyKey!, SquarePaymentId = row.SquarePaymentId,
                SquareCheckoutId = row.SquareCheckoutId, BaseAmount = row.BaseAmount,
                SurchargeAmount = row.SurchargeAmount, ChargedAmount = row.ChargedAmount, PaymentAttemptId = row.AttemptId,
            }, cancellationToken).ConfigureAwait(false);
        return saved.Ok ? SquareRecoveryUpdateResult.Success() : SquareRecoveryUpdateResult.Fail(saved.ErrorMessage ?? "Top-up could not be saved.");
    }
}
