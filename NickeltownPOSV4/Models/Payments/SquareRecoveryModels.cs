using System;

namespace NickeltownPOSV4.Models.Payments;

public static class SquareRecoveryStatuses
{
    public const string PendingReview = "PendingReview";
    public const string ManuallyReconciled = "ManuallyReconciled";
    public const string LinkedPitstop = "LinkedPitstop";
    public const string LinkedTab = "LinkedTab";
    public const string Ignored = "Ignored";
}

public sealed class SquareRecoveryRow
{
    public long AttemptId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string Status { get; init; } = string.Empty;

    public string PaymentType { get; init; } = string.Empty;

    public string? TabLegacyId { get; init; }

    public decimal BaseAmount { get; init; }

    public decimal SurchargeAmount { get; init; }

    public decimal ChargedAmount { get; init; }

    public string? SquarePaymentId { get; init; }

    public string? SquareCheckoutId { get; init; }

    public string? IdempotencyKey { get; init; }

    public string? InitiatedByStaffName { get; init; }

    public string? FailureReason { get; init; }

    public string? RecoveryStatus { get; init; }

    public string? RecoveryNote { get; init; }

    public bool HasRecoverablePayload { get; init; }
}

public sealed class SquareRecoveryUpdateResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public static SquareRecoveryUpdateResult Success() => new() { Ok = true };

    public static SquareRecoveryUpdateResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}
