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

    public Task<SquarePresentResult> PresentPaymentRequestAsync(
        SquarePaymentRequest paymentRequest,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null) =>
        PresentCardChargeAsync(paymentRequest.TotalAmount, cancellationToken, paymentRequest.Note);
}
