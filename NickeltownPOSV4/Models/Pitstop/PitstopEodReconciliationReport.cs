using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Pitstop;

/// <summary>Pitstop terminal retail reconciliation for EOD (excludes bar tabs).</summary>
public sealed class PitstopEodReconciliationReport
{
    public string PeriodCaption { get; init; } = string.Empty;

    public decimal CashSales { get; init; }

    public decimal CardSalesCharged { get; init; }

    public decimal CardBaseProductTotal { get; init; }

    public decimal CardSurchargeCollected { get; init; }

    public decimal EstimatedSquareFee { get; init; }

    public decimal NetCardAfterFees { get; init; }

    public decimal TotalRecordedSales { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}