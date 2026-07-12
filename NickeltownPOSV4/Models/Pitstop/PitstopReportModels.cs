using System;
using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Pitstop;

/// <summary>Editable outside sales row (merch, food, snacks, etc.).</summary>
public sealed class OutsideItemSaleRow
{
    public string Key { get; init; } = string.Empty;

    public string DisplayLabel { get; init; } = string.Empty;

    /// <summary>V2-aligned discriminator: <see cref="PitstopOutsideLineCatalogBuilder.LineKindMerchSku"/> etc.</summary>
    public string OutsideLineKind { get; init; } = string.Empty;

    /// <summary>Catalog item when the row is backed by Pitstop stock (merch match or food SKU).</summary>
    public long? PitstopItemId { get; init; }

    /// <summary>Optional Pitstop unit price hint (V2 computed columns); raffle uses $2 when not in catalog.</summary>
    public decimal? SuggestedUnitPrice { get; init; }

    public int CashQty { get; set; }

    public decimal CashDollars { get; set; }

    /// <summary>Manual outside card qty when Square import is unavailable.</summary>
    public int CardQty { get; set; }

    /// <summary>Manual outside card total when Square import is unavailable.</summary>
    public decimal CardDollars { get; set; }
}

/// <summary>Combined outside sales: paper-sheet cash plus Square card order lines.</summary>
public sealed class CombinedOutsideSaleRow
{
    public long? PitstopItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public int CashQuantity { get; init; }

    public decimal CashTotal { get; init; }

    public int CardQuantity { get; init; }

    public decimal CardTotal { get; init; }

    public int CombinedQuantity => CashQuantity + CardQuantity;

    public decimal CombinedTotal => CashTotal + CardTotal;
}

public sealed class EventExpenseRow
{
    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

public sealed class MerchPrizeGiveawayRow
{
    public long ItemId { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; set; }
}

/// <summary>User-entered + database-backed inputs for end-of-day Pitstop reporting.</summary>
public sealed class PitstopReportInputs
{
    public string EventName { get; set; } = string.Empty;

    public DateTimeOffset PeriodStartLocal { get; set; }

    public DateTimeOffset PeriodEndLocal { get; set; }

    public string? StaffName { get; set; }

    /// <summary>Automatic Square reconciliation for the report period.</summary>
    public SquarePaymentReconciliationResult? SquareReconciliation { get; set; }

    /// <summary>Manual total Square card gross for the event day when automatic import fails.</summary>
    public decimal? ManualCombinedSquareCardGross { get; set; }

    /// <summary>Processor fee percent used when Square does not return fees, e.g. 1.75 means 1.75%.</summary>
    public decimal SquareFeePercent { get; set; } = 1.75m;

    public decimal InsideFloat { get; set; }

    public decimal OutsideFloat { get; set; }

    public decimal OtherFoodDrinkCash { get; set; }

    public decimal OtherFoodDrinkCard { get; set; }

    public int RaffleCashQty { get; set; }

    public decimal RaffleCashDollars { get; set; }

    public int RaffleCardQty { get; set; }

    public decimal RaffleCardDollars { get; set; }

    public List<OutsideItemSaleRow> OutsideLines { get; } = new();

    public List<EventExpenseRow> Expenses { get; } = new();

    public List<MerchPrizeGiveawayRow> PrizeGiveaways { get; } = new();

    /// <summary>Optional manual override when POS history is incomplete.</summary>
    public decimal? InsideCashOverride { get; set; }

    public decimal? InsideCardOverride { get; set; }

    /// <summary>Reconciliation/EOD warnings to show on the PDF (Square recovery, voids, missing refs, etc.).</summary>
    public List<string> Warnings { get; } = new();

    public decimal? CashCounted { get; set; }

    public decimal? FloatRemoved { get; set; }

    /// <summary>When true, POS totals and itemised lines use sample data instead of the database.</summary>
    public bool UseTestPosData { get; set; }
}

/// <summary>Full calculated snapshot for UI preview + PDF.</summary>
public sealed class PitstopReportData
{
    public string EventName { get; init; } = string.Empty;

    public string PeriodCaption { get; init; } = string.Empty;

    public string? StaffName { get; init; }

    public decimal InsideCashFromPos { get; init; }

    public decimal InsideCardFromPos { get; init; }

    public decimal PitstopRetailCash { get; init; }

    /// <summary>Pitstop card totals as charged / settled on Square Terminal (includes pass-through surcharge when present).</summary>
    public decimal PitstopRetailCard { get; init; }

    /// <summary>Product subtotal for Pitstop card sales (before pass-through surcharge).</summary>
    public decimal PitstopCardBaseProductTotal { get; init; }

    public decimal PitstopCardSurchargeCollected { get; init; }

    /// <summary>Pitstop terminal card charged (Square reconciliation anchor; tabs excluded).</summary>
    public decimal InsidePosCardTotalForReconciliation { get; init; }

    /// <summary>Outside terminal card gross imported from Square. Kept for archive compatibility.</summary>
    public decimal OutsideMerchRaffleCardTotal { get; init; }

    public decimal CombinedSquareCardGross { get; init; }

    public decimal PosSquareGross { get; init; }

    public decimal OutsideSquareGross { get; init; }

    public int PosSquareTransactionCount { get; init; }

    public int OutsideSquareTransactionCount { get; init; }

    public decimal? ActualSquareFees { get; init; }

    public decimal ExpectedSquareDeposit { get; init; }

    public bool SquareReconciliationLoaded { get; init; }

    public bool UsingManualSquareCardFallback { get; init; }

    public string? SquareReconciliationError { get; init; }

    public IReadOnlyList<SquareReconciliationPaymentRow> SquareMatchedPayments { get; init; } = Array.Empty<SquareReconciliationPaymentRow>();

    public IReadOnlyList<SquareReconciliationPaymentRow> SquareUnmatchedPayments { get; init; } = Array.Empty<SquareReconciliationPaymentRow>();

    public IReadOnlyList<SquareMissingLocalPaymentRow> SquareMissingLocalPayments { get; init; } = Array.Empty<SquareMissingLocalPaymentRow>();

    public decimal OutsideCardDerived { get; init; }

    public decimal OutsideCardItemisedBase { get; init; }

    public decimal OutsideCardDifference { get; init; }

    public bool OutsideCardMismatch { get; init; }

    public decimal OutsideCashTotal { get; init; }

    public decimal TotalCashGross { get; init; }

    public decimal TotalCardGross { get; init; }

    public decimal GrossSales { get; init; }

    public decimal TotalExpenses { get; init; }

    public decimal EstimatedSquareFees { get; init; }

    public decimal CashToDeposit { get; init; }

    public decimal NetEventProfit { get; init; }

    public decimal InsideFloat { get; init; }

    public decimal OutsideFloat { get; init; }

    public decimal SquareFeePercent { get; init; }

    public IReadOnlyList<OutsideItemSaleRow> OutsideLines { get; init; } = Array.Empty<OutsideItemSaleRow>();

    public IReadOnlyList<CombinedOutsideSaleRow> CombinedOutsideSales { get; init; } = Array.Empty<CombinedOutsideSaleRow>();

    public IReadOnlyList<EventExpenseRow> Expenses { get; init; } = Array.Empty<EventExpenseRow>();

    public IReadOnlyList<MerchPrizeGiveawayRow> PrizeGiveaways { get; init; } = Array.Empty<MerchPrizeGiveawayRow>();

    public IReadOnlyList<PitstopProductAggregateRow> PitstopProductSales { get; init; } = Array.Empty<PitstopProductAggregateRow>();

    public IReadOnlyList<PitstopCategoryAggregateRow> PitstopCategorySales { get; init; } = Array.Empty<PitstopCategoryAggregateRow>();

    public IReadOnlyList<PitstopProductAggregateRow> OutsideTerminalProductSales { get; init; } = Array.Empty<PitstopProductAggregateRow>();

    public IReadOnlyList<PitstopCategoryAggregateRow> OutsideTerminalCategorySales { get; init; } = Array.Empty<PitstopCategoryAggregateRow>();

    public IReadOnlyList<PitstopProductAggregateRow> CombinedEventProductSales { get; init; } = Array.Empty<PitstopProductAggregateRow>();

    public IReadOnlyList<PitstopCategoryAggregateRow> CombinedEventCategorySales { get; init; } = Array.Empty<PitstopCategoryAggregateRow>();

    public IReadOnlyList<EventCategoryComparisonRow> EventCategoryComparison { get; init; } = Array.Empty<EventCategoryComparisonRow>();

    public IReadOnlyList<PitstopPaymentBreakdownRow> PitstopPaymentBreakdown { get; init; } = Array.Empty<PitstopPaymentBreakdownRow>();

    public decimal OtherFoodDrinkCash { get; init; }

    public decimal OtherFoodDrinkCard { get; init; }

    public int RaffleCashQty { get; init; }

    public decimal RaffleCashDollars { get; init; }

    public int RaffleCardQty { get; init; }

    public decimal RaffleCardDollars { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public decimal? CashCounted { get; init; }

    public decimal? FloatRemoved { get; init; }

    public decimal? ExpectedCash { get; init; }

    public decimal? CashVariance { get; init; }

    public bool IsTestReport { get; init; }
}

public sealed class PitstopProductAggregateRow
{
    public long ItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal LineTotal { get; init; }
}

public sealed class PitstopCategoryAggregateRow
{
    public string CategoryName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal LineTotal { get; init; }
}

public sealed class PitstopPaymentBreakdownRow
{
    public string PaymentMethod { get; init; } = string.Empty;

    public decimal Total { get; init; }
}
