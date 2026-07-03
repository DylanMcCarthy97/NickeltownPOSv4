using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class PitstopSaleLineCommit
{
    public long ItemId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? Sku { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }

    public string? CategoryName { get; init; }

    public string? SubCategory { get; init; }
}

public sealed class PitstopSalePaymentCommit
{
    public string PaymentMethod { get; init; } = string.Empty;

    public long? BartenderId { get; init; }

    public string? StaffDisplayName { get; init; }

    public string? SquareExternalRef { get; init; }

    /// <summary>Sum of product line totals (before any card surcharge line).</summary>
    public decimal BaseProductTotal { get; init; }

    public decimal? CardSurchargePercent { get; init; }

    public decimal? CardSurchargeAmount { get; init; }

    /// <summary>Cash: equals <see cref="BaseProductTotal"/>. Card: base + surcharge (amount charged on Square Terminal).</summary>
    public decimal ChargedTotal { get; init; }

    public string? IdempotencyKey { get; init; }

    public string? SquareCheckoutId { get; init; }

    public decimal? CashReceived { get; init; }

    public decimal? CashChange { get; init; }

    public long? PaymentAttemptId { get; init; }
}

public sealed class PitstopSaleCommitResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public long? SalePk { get; init; }

    public string? SaleGuid { get; init; }

    public static PitstopSaleCommitResult Success(long salePk, string saleGuid) =>
        new() { Ok = true, SalePk = salePk, SaleGuid = saleGuid };

    public static PitstopSaleCommitResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

public sealed class PitstopRetailPeriodTotals
{
    public decimal CashTotal { get; init; }

    /// <summary>Card amounts actually charged / recorded on <c>PitstopSales.Total</c> for card payments.</summary>
    public decimal CardChargedTotal { get; init; }

    /// <summary>Product value for Pitstop card sales (before pass-through surcharge).</summary>
    public decimal CardBaseProductTotal { get; init; }

    public decimal CardSurchargeCollected { get; init; }
}

public sealed class PitstopActiveSaleRow
{
    public long SaleId { get; init; }

    public DateTimeOffset SoldAt { get; init; }

    public decimal Total { get; init; }

    public string PaymentMethod { get; init; } = string.Empty;

    public string? StaffDisplayName { get; init; }

    public string? SquareExternalRef { get; init; }

    public string Status { get; init; } = "Active";

    public bool StockWasDeducted { get; init; }
}

public sealed class PitstopVoidSaleRequest
{
    public long SaleId { get; init; }

    public long? StaffId { get; init; }

    public string? StaffDisplayName { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public sealed class PitstopVoidSaleResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public bool StockRestored { get; init; }

    public decimal AmountVoided { get; init; }

    public bool WasSquareSale { get; init; }

    public bool WasArchived { get; init; }

    public static PitstopVoidSaleResult Success(bool stockRestored, decimal amount, bool wasSquare, bool wasArchived) =>
        new()
        {
            Ok = true,
            StockRestored = stockRestored,
            AmountVoided = amount,
            WasSquareSale = wasSquare,
            WasArchived = wasArchived,
        };

    public static PitstopVoidSaleResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

/// <summary>Records Pitstop retail checkout sales (separate from bar tabs) and adjusts stock.</summary>
public interface IPitstopRetailSaleRepository
{
    Task<PitstopSaleCommitResult> CommitSaleAsync(
        IReadOnlyList<PitstopSaleLineCommit> lines,
        PitstopSalePaymentCommit payment,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PitstopSaleLineReportRow>> GetItemisedLinesAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default);

    Task<PitstopRetailPeriodTotals> GetPitstopRetailPaymentTotalsAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>Obsolete — Pitstop sales are archived, not deleted. Always throws <see cref="NotSupportedException"/>.</summary>
    Task<PitstopDaySalesClearResult> ClearPitstopRetailSalesForPeriodAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>Lists Active (non-voided, non-archived) Pitstop sales for void selection.</summary>
    Task<IReadOnlyList<PitstopActiveSaleRow>> GetActivePitstopSalesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the count of Pitstop sales voided within the given window (excludes already-voided-prior sales).</summary>
    Task<int> GetVoidedPitstopSaleCountForPeriodAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an active Pitstop sale as Voided. If the original sale deducted stock,
    /// restores stock and writes a positive StockMovement. Never deletes the sale row.
    /// </summary>
    Task<PitstopVoidSaleResult> VoidPitstopSaleAsync(
        PitstopVoidSaleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PitstopDaySalesClearResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public int SalesRemoved { get; init; }

    public static PitstopDaySalesClearResult Success(int salesRemoved) =>
        new() { Ok = true, SalesRemoved = salesRemoved };

    public static PitstopDaySalesClearResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

public sealed class PitstopSaleLineReportRow
{
    public long ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string? CategoryName { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
