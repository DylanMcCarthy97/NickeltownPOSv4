using System;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

/// <summary>Deterministic delay + approve for kiosk dev until Square Terminal API is wired.</summary>
public sealed class SimulatedSquareTerminalSession : ISquareTerminalSession
{
    public async Task<SquarePresentResult> PresentCardChargeAsync(
        decimal amount,
        CancellationToken cancellationToken = default,
        string? checkoutNote = null)
    {
        if (amount <= 0m)
        {
            return SquarePresentResult.Declined("Amount must be positive.");
        }

        await Task.Delay(650, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var id = "sim_sq_" + Guid.NewGuid().ToString("N")[..20];
        return SquarePresentResult.ApprovedSim(id);
    }

    public async Task<SquarePresentResult> PresentPaymentRequestAsync(
        SquarePaymentRequest paymentRequest,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null,
        Func<string, Task>? checkoutCreated = null)
    {
        var checkoutId = "sim_checkout_" + (idempotencyKey ?? Guid.NewGuid().ToString("N"));
        if (checkoutCreated is not null)
        {
            await checkoutCreated(checkoutId).ConfigureAwait(false);
        }
        var result = await PresentCardChargeAsync(paymentRequest.TotalAmount, cancellationToken, paymentRequest.Note).ConfigureAwait(false);
        return result.Approved
            ? SquarePresentResult.ApprovedSim(result.ExternalTransactionId!, checkoutId, idempotencyKey)
            : result;
    }
}
