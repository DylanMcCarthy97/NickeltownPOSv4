using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Payments;

public sealed class SquareCardPaymentRequest
{
    public SquarePaymentAttemptType PaymentType { get; init; }

    /// <summary>When set, reuses this key for idempotency (Pitstop transaction GUID). Otherwise a new key is generated.</summary>
    public string? IdempotencyKey { get; init; }

    public string? TabLegacyId { get; init; }

    public decimal BaseAmount { get; init; }

    public decimal SurchargeAmount { get; init; }

    public decimal ChargedAmount { get; init; }

    public SquarePaymentRequest TerminalRequest { get; init; } = new();
}

public sealed class SquareCardPaymentOutcome
{
    public bool Approved { get; init; }

    public bool AlreadyRecorded { get; init; }

    public string? DeclineReason { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;

    public string? SquarePaymentId { get; init; }

    public string? SquareCheckoutId { get; init; }

    public long? AttemptId { get; init; }
}

public interface ISquareCardPaymentOrchestrator
{
    Task<SquareCardPaymentOutcome> PresentAndLogAsync(
        SquareCardPaymentRequest request,
        CancellationToken cancellationToken = default);
}