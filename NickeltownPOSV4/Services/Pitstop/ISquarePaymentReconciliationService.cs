using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Services.Pitstop;

public interface ISquarePaymentReconciliationService
{
    /// <param name="includeOutsideTerminal">
    /// When false, match POS / Pitstop sales only and skip Flounderers02 outside enrichment.
    /// </param>
    Task<SquarePaymentReconciliationResult> ReconcileAsync(
        DateTimeOffset periodStartLocal,
        DateTimeOffset periodEndLocal,
        decimal squareFeePercentFallback,
        CancellationToken cancellationToken = default,
        bool includeOutsideTerminal = true);
}