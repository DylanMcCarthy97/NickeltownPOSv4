using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Services.Pitstop;

public interface ISquarePaymentReconciliationService
{
    Task<SquarePaymentReconciliationResult> ReconcileAsync(
        DateTimeOffset periodStartLocal,
        DateTimeOffset periodEndLocal,
        decimal squareFeePercentFallback,
        CancellationToken cancellationToken = default);
}