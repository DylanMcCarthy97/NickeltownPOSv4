using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>
/// Pure Pitstop EOD money math. Kept decimal-safe and UI-agnostic so the
/// touchscreen workflow can change without rewriting the accounting rules.
/// </summary>
public static class PitstopEodCalculator
{
    public const decimal MismatchTolerance = 0.05m;

    public static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal LineSales(int quantity, decimal? unitPrice)
    {
        if (quantity <= 0 || unitPrice is not decimal price || price <= 0m)
        {
            return 0m;
        }

        return RoundMoney(quantity * price);
    }

    public static decimal OutsideItemSalesTotal(IEnumerable<OutsideItemSaleRow> lines)
    {
        var total = 0m;
        foreach (var line in lines)
        {
            total += LineSales(SoldQuantity(line), ResolveUnitPrice(line));
        }

        return RoundMoney(total);
    }

    public static int SoldQuantity(OutsideItemSaleRow line) =>
        Math.Max(0, line.SoldQty > 0 ? line.SoldQty : line.CashQty + line.CardQty);

    public static decimal? ResolveUnitPrice(OutsideItemSaleRow line)
    {
        if (line.SuggestedUnitPrice is decimal p && p > 0m)
        {
            return p;
        }

        var qty = SoldQuantity(line);
        var dollars = line.CashDollars + line.CardDollars;
        if (qty > 0 && dollars > 0m)
        {
            return RoundMoney(dollars / qty);
        }

        return null;
    }

    public static (decimal cardSales, decimal surcharge) ActualOutsideCardSales(
        decimal outsideSquareGross,
        decimal surchargePercent) =>
        SquareCardFeeCalculator.SplitGrossExcludingSurcharge(outsideSquareGross, surchargePercent);

    public static decimal OutsideCashSales(decimal itemSalesTotal, decimal actualCardSales) =>
        RoundMoney(itemSalesTotal - actualCardSales);

    public static bool IsOutsideTenderMismatch(decimal itemSalesTotal, decimal actualCardSales) =>
        actualCardSales - itemSalesTotal > MismatchTolerance;

    public static decimal ExpectedCash(decimal startingFloat, decimal cashSales, decimal cashPaidOut) =>
        RoundMoney(startingFloat + cashSales - cashPaidOut);

    public static decimal? CashVariance(decimal? counted, decimal expected) =>
        counted is decimal value ? RoundMoney(value - expected) : null;

    public static string VarianceStatus(decimal? variance)
    {
        if (variance is null)
        {
            return string.Empty;
        }

        var amount = variance.Value;
        if (Math.Abs(amount) < 0.01m)
        {
            return "BALANCED";
        }

        return amount > 0m
            ? $"{Math.Abs(amount):0.00} OVER"
            : $"{Math.Abs(amount):0.00} SHORT";
    }

    public static bool IsBalanced(decimal? variance) =>
        variance is decimal v && Math.Abs(v) < 0.01m;

    public static decimal CashToBankFromCount(decimal counted, decimal startingFloatRetained) =>
        RoundMoney(counted - startingFloatRetained);

    public static decimal CashToBankEstimate(decimal cashSales, decimal cashPaidOut) =>
        RoundMoney(cashSales - cashPaidOut);

    public static decimal TillCashToBank(
        decimal? counted,
        decimal startingFloat,
        decimal cashSales,
        decimal cashPaidOut) =>
        counted is decimal value
            ? CashToBankFromCount(value, startingFloat)
            : CashToBankEstimate(cashSales, cashPaidOut);

    public static decimal CashPaidOut(
        IEnumerable<EventExpenseRow> lines,
        EventExpensePaymentSource source) =>
        RoundMoney(
            lines
                .Where(e => e.PaidFrom == source && e.Amount != 0m)
                .Sum(e => e.Amount));

    public static decimal TotalCashPrizes(IEnumerable<EventExpenseRow> lines) =>
        RoundMoney(
            lines
                .Where(e => e.Kind == EventExpenseKind.CashPrize)
                .Sum(e => e.Amount));

    public static decimal TotalExpenses(IEnumerable<EventExpenseRow> lines) =>
        RoundMoney(
            lines
                .Where(e => e.Kind != EventExpenseKind.CashPrize)
                .Sum(e => e.Amount));

    public static decimal TotalSales(decimal insideCash, decimal insideCardProduct, decimal outsideItemSales) =>
        RoundMoney(insideCash + insideCardProduct + outsideItemSales);

    public static decimal TotalCashSales(decimal insideCash, decimal outsideCash) =>
        RoundMoney(insideCash + outsideCash);

    public static decimal TotalCardSales(decimal insideCardProduct, decimal outsideCardSales) =>
        RoundMoney(insideCardProduct + outsideCardSales);

    public static (decimal knownCosts, bool hasUnknown) KnownStockCosts(
        IEnumerable<StockCostLine> lines)
    {
        var known = 0m;
        var hasUnknown = false;
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                continue;
            }

            if (line.UnitCost is decimal cost && cost > 0m)
            {
                known += RoundMoney(line.Quantity * cost);
            }
            else
            {
                hasUnknown = true;
            }
        }

        return (RoundMoney(known), hasUnknown);
    }

    public static decimal EstimatedProfit(
        decimal totalSales,
        decimal expenses,
        decimal cashPrizes,
        decimal knownStockCosts,
        decimal squareProcessingFees) =>
        RoundMoney(totalSales - expenses - cashPrizes - knownStockCosts - squareProcessingFees);

    public static PitstopEodCloseCheckpoint NextCloseRetry(PitstopEodCloseState state)
    {
        if (!state.PdfSaved)
        {
            return PitstopEodCloseCheckpoint.SavePdf;
        }

        if (!state.Archived && !state.SkipArchive)
        {
            return PitstopEodCloseCheckpoint.Archive;
        }

        if (!state.StockApplied && !state.SkipStock)
        {
            return PitstopEodCloseCheckpoint.ApplyStock;
        }

        return PitstopEodCloseCheckpoint.Complete;
    }

    public static bool IsDuplicateFinalisation(bool alreadyArchivedThisSession, bool periodAlreadyArchived) =>
        alreadyArchivedThisSession || periodAlreadyArchived;
}

public readonly struct StockCostLine
{
    public StockCostLine(int quantity, decimal? unitCost)
    {
        Quantity = quantity;
        UnitCost = unitCost;
    }

    public int Quantity { get; }

    public decimal? UnitCost { get; }
}

public enum PitstopEodCloseCheckpoint
{
    SavePdf = 0,
    Archive = 1,
    ApplyStock = 2,
    Complete = 3,
}

public sealed class PitstopEodCloseState
{
    public bool PdfSaved { get; set; }

    public bool Archived { get; set; }

    public bool StockApplied { get; set; }

    public bool SkipArchive { get; set; }

    public bool SkipStock { get; set; }

    public string? PdfPath { get; set; }

    public long? BatchId { get; set; }

    public string? LastFailure { get; set; }

    public PitstopEodCloseCheckpoint Next => PitstopEodCalculator.NextCloseRetry(this);

    public bool IsComplete => Next == PitstopEodCloseCheckpoint.Complete;

    public void Reset()
    {
        PdfSaved = false;
        Archived = false;
        StockApplied = false;
        SkipArchive = false;
        SkipStock = false;
        PdfPath = null;
        BatchId = null;
        LastFailure = null;
    }
}
