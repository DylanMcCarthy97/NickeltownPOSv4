using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Reconciliation;

public sealed class EodReconciliationLine
{
    public string Source { get; init; } = string.Empty;

    public string PaymentType { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string? SquarePaymentId { get; init; }

    public string? SquareCheckoutId { get; init; }

    public string? LocalReference { get; init; }
}

public sealed class EodReconciliationReport
{
    public string PeriodCaption { get; init; } = string.Empty;

    public decimal BarTabCashPayments { get; init; }

    public decimal BarTabSquarePayments { get; init; }

    public decimal BarTabSquareSurchargeCollected { get; init; }

    public decimal PitstopCashSales { get; init; }

    public decimal PitstopCardSales { get; init; }

    public decimal PitstopCardSurchargeCollected { get; init; }

    public decimal TotalCardSurchargeCollected { get; init; }

    public decimal EstimatedSquareFee { get; init; }

    public decimal NetCardAfterFees { get; init; }

    public decimal TotalCashExpected { get; init; }

    public decimal TotalPosRecordedSales { get; init; }

    public IReadOnlyList<EodReconciliationLine> SquareReferenceLines { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}