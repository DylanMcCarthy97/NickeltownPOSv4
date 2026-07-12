using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Models.Pitstop;

public sealed class SquareOrderLineItemRow
{
    public string ItemName { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitPrice { get; init; }

    public decimal LineTotal { get; init; }

    public string? CatalogObjectId { get; init; }

    public string? SquareCategoryName { get; init; }

    public long? MappedClubPosItemId { get; init; }

    public string? MappedClubPosItemName { get; init; }

    public bool MappedToClubPos { get; init; }
}

public sealed class SquareReconciliationPaymentRow
{
    public string PaymentId { get; init; } = string.Empty;

    public string? OrderId { get; init; }

    public DateTimeOffset PaidAt { get; init; }

    public decimal GrossAmount { get; init; }

    public string? ReceiptNumber { get; init; }

    public string? DeviceName { get; init; }

    public string? CardLast4 { get; init; }

    public SquarePaymentTerminalClass TerminalClass { get; init; }

    public long? LocalSaleId { get; init; }

    public string? LocalSaleRef { get; init; }

    public decimal? LocalSaleAmount { get; init; }

    public bool OrderLoaded { get; init; }

    public string? OrderLoadWarning { get; init; }

    public IReadOnlyList<SquareOrderLineItemRow> LineItems { get; init; } = Array.Empty<SquareOrderLineItemRow>();
}

public enum SquarePaymentTerminalClass
{
    PosTerminal,
    OutsideTerminal,
}

public sealed class SquareMissingLocalPaymentRow
{
    public long SaleId { get; init; }

    public string SaleRef { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string PaymentId { get; init; } = string.Empty;
}

public sealed class SquarePaymentReconciliationResult
{
    public decimal PosSquareGross { get; init; }

    public decimal OutsideSquareGross { get; init; }

    public decimal CombinedSquareGross { get; init; }

    public int PosTransactionCount { get; init; }

    public int OutsideTransactionCount { get; init; }

    public decimal? ActualSquareFees { get; init; }

    public decimal ExpectedSquareDeposit { get; init; }

    public bool LoadedFromSquare { get; init; }

    public string? LoadError { get; init; }

    public IReadOnlyList<SquareReconciliationPaymentRow> MatchedPayments { get; init; } = Array.Empty<SquareReconciliationPaymentRow>();

    public IReadOnlyList<SquareReconciliationPaymentRow> UnmatchedSquarePayments { get; init; } = Array.Empty<SquareReconciliationPaymentRow>();

    public IReadOnlyList<SquareMissingLocalPaymentRow> MissingLocalPayments { get; init; } = Array.Empty<SquareMissingLocalPaymentRow>();

    public IReadOnlyList<PitstopProductAggregateRow> OutsideTerminalProductSales { get; init; } = Array.Empty<PitstopProductAggregateRow>();

    public IReadOnlyList<PitstopCategoryAggregateRow> OutsideTerminalCategorySales { get; init; } = Array.Empty<PitstopCategoryAggregateRow>();

    public int OutsideOrdersLoadedCount { get; init; }

    public int OutsideOrdersMissingCount { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static SquarePaymentReconciliationResult Empty(string? error = null) =>
        new() { LoadedFromSquare = false, LoadError = error };
}

public sealed class PitstopCardSaleRefRow
{
    public long SaleId { get; init; }

    public string SaleRef { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public string SquareExternalRef { get; init; } = string.Empty;

    public DateTimeOffset SoldAt { get; init; }
}

public sealed class EventCategoryComparisonRow
{
    public string CategoryName { get; init; } = string.Empty;

    public int ClubPosQuantity { get; init; }

    public decimal ClubPosLineTotal { get; init; }

    public int OutsideTerminalQuantity { get; init; }

    public decimal OutsideTerminalLineTotal { get; init; }

    public int CombinedQuantity { get; init; }

    public decimal CombinedLineTotal { get; init; }
}
