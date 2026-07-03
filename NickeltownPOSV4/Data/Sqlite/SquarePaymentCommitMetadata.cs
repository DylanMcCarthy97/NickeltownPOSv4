namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SquarePaymentCommitMetadata
{
    public string IdempotencyKey { get; init; } = string.Empty;

    public string? SquarePaymentId { get; init; }

    public string? SquareCheckoutId { get; init; }

    public decimal BaseAmount { get; init; }

    public decimal SurchargeAmount { get; init; }

    public decimal ChargedAmount { get; init; }

    public long? PaymentAttemptId { get; init; }
}