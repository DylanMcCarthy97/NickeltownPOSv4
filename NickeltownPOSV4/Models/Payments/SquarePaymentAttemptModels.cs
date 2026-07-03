namespace NickeltownPOSV4.Models.Payments;

public enum SquarePaymentAttemptType
{
    TabTopUp,
    PitstopSale,
}

public enum SquarePaymentAttemptStatus
{
    Pending,
    TerminalApproved,
    Completed,
    Failed,
    Cancelled,
    TimedOut,
}

public sealed class SquarePaymentAttemptBeginRequest
{
    public SquarePaymentAttemptType PaymentType { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;

    public string? TabLegacyId { get; init; }

    public decimal BaseAmount { get; init; }

    public decimal SurchargeAmount { get; init; }

    public decimal ChargedAmount { get; init; }
}

public sealed class SquarePaymentAttemptBeginResult
{
    public long AttemptId { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;

    public bool AlreadyCompleted { get; init; }

    /// <summary>Terminal already approved this idempotency key; skip a second Square presentation.</summary>
    public bool AlreadyTerminalApproved { get; init; }

    public string? SquarePaymentId { get; init; }

    public string? SquareCheckoutId { get; init; }
}