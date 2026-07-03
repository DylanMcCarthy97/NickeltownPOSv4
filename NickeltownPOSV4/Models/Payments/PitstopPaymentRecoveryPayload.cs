using System;
using System.Collections.Generic;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Models.Payments;

public sealed class PitstopPaymentRecoveryPayload
{
    public string TransactionGuid { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public List<PitstopSaleLineCommit> Lines { get; init; } = new();
    public PitstopSalePaymentCommit Payment { get; init; } = new();
    public long? StaffId { get; init; }
    public string? StaffDisplayName { get; init; }
}

public sealed class PaymentRecoveryAlertSummary
{
    public long AttemptId { get; init; }
    public decimal ChargedAmount { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string TransactionId { get; init; } = string.Empty;
    public string PaymentType { get; init; } = string.Empty;
}
