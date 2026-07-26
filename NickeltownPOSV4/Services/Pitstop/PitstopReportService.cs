using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>Pitstop-only end-of-day figures (terminal retail + outside merch/raffle). Bar tabs are excluded.</summary>
public sealed class PitstopReportService
{
    private const decimal MismatchTolerance = 0.05m;

    private readonly IPitstopRetailSaleRepository _pitstopSales;

    public PitstopReportService(IPitstopRetailSaleRepository pitstopSales) => _pitstopSales = pitstopSales;

    public async Task<PitstopReportData> BuildAsync(PitstopReportInputs inputs, CancellationToken cancellationToken = default)
    {
        var start = inputs.PeriodStartLocal;
        var end = inputs.PeriodEndLocal;
        if (end <= start)
        {
            end = start.AddDays(1);
        }

        var pitstopTotals = inputs.UseTestPosData
            ? PitstopReportTestDataBuilder.BuildPosTotals()
            : await _pitstopSales.GetPitstopRetailPaymentTotalsAsync(start, end, cancellationToken).ConfigureAwait(false);
        var lines = inputs.UseTestPosData
            ? PitstopReportTestDataBuilder.BuildPosLines()
            : await _pitstopSales.GetItemisedLinesAsync(start, end, cancellationToken).ConfigureAwait(false);

        var pitCash = pitstopTotals.CashTotal;
        var pitCardCharged = pitstopTotals.CardChargedTotal;
        var pitCardBase = pitstopTotals.CardBaseProductTotal;
        var pitCardSurcharge = pitstopTotals.CardSurchargeCollected;

        var outsideCash = inputs.OutsideLines.Sum(r => r.CashDollars);

        var square = inputs.SquareReconciliation ?? SquarePaymentReconciliationResult.Empty("Square reconciliation has not been loaded.");
        var manualCombined = inputs.ManualCombinedSquareCardGross;
        var usingManualMode = inputs.UseManualSquareCardMode;
        var usingManualFallback = usingManualMode && manualCombined is > 0m;

        decimal combinedSquare;
        decimal posSquare;
        decimal outsideSquare;
        if (usingManualMode)
        {
            posSquare = decimal.Round(pitCardCharged, 2, MidpointRounding.AwayFromZero);
            if (usingManualFallback)
            {
                combinedSquare = decimal.Round(manualCombined!.Value, 2, MidpointRounding.AwayFromZero);
                outsideSquare = decimal.Round(Math.Max(0m, combinedSquare - pitCardCharged), 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                // Manual mode before a combined total is entered: POS only, no outside Square.
                combinedSquare = posSquare;
                outsideSquare = 0m;
            }
        }
        else
        {
            combinedSquare = decimal.Round(square.CombinedSquareGross, 2, MidpointRounding.AwayFromZero);
            posSquare = decimal.Round(square.PosSquareGross, 2, MidpointRounding.AwayFromZero);
            outsideSquare = decimal.Round(square.OutsideSquareGross, 2, MidpointRounding.AwayFromZero);
        }

        var squareVsTerminalDiff = decimal.Round(combinedSquare - pitCardCharged, 2, MidpointRounding.AwayFromZero);
        // Red mismatch banner is amount-only. Square warnings (e.g. excluded bar-terminal
        // payments, outside sales) stay in Warnings / ReconciliationWarnings separately.
        var squareMismatch = usingManualFallback
            ? combinedSquare < pitCardCharged - MismatchTolerance
            : !usingManualMode && Math.Abs(posSquare - pitCardCharged) > MismatchTolerance;

        var feePct = inputs.SquareFeePercent;
        // In manual mode fees are estimated from the combined total — do not use Square day fees
        // that may include outside-terminal volume we intentionally ignored.
        var fees = !usingManualMode && square.ActualSquareFees is decimal actualFees
            ? decimal.Round(actualFees, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(combinedSquare * (feePct / 100m), 2, MidpointRounding.AwayFromZero);
        var expectedDeposit = !usingManualMode && square.LoadedFromSquare
            ? decimal.Round(square.ExpectedSquareDeposit, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(combinedSquare - fees, 2, MidpointRounding.AwayFromZero);

        var totalCash = decimal.Round(pitCash + outsideCash, 2, MidpointRounding.AwayFromZero);
        var totalExpenses = decimal.Round(inputs.Expenses.Sum(e => e.Amount), 2, MidpointRounding.AwayFromZero);
        var insidePaidOut = decimal.Round(
            inputs.Expenses
                .Where(e => e.PaidFrom == EventExpensePaymentSource.InsideTill)
                .Sum(e => e.Amount),
            2,
            MidpointRounding.AwayFromZero);
        var outsidePaidOut = decimal.Round(
            inputs.Expenses
                .Where(e => e.PaidFrom == EventExpensePaymentSource.OutsideTin)
                .Sum(e => e.Amount),
            2,
            MidpointRounding.AwayFromZero);

        var insideExpected = decimal.Round(inputs.InsideFloat + pitCash - insidePaidOut, 2, MidpointRounding.AwayFromZero);
        var outsideExpected = decimal.Round(inputs.OutsideFloat + outsideCash - outsidePaidOut, 2, MidpointRounding.AwayFromZero);
        var insideVariance = inputs.CashCounted is decimal insideCounted
            ? decimal.Round(insideCounted - insideExpected, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        var outsideVariance = inputs.OutsideCashCounted is decimal outsideCounted
            ? decimal.Round(outsideCounted - outsideExpected, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        // A physical count is authoritative. Before a count is entered, show the sales-based
        // estimate (cash sales less cash paid out); floats were never part of sales and must
        // not be subtracted from that estimate.
        var insideToBank = inputs.CashCounted is decimal insideCash
            ? decimal.Round(insideCash - inputs.InsideFloat, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(pitCash - insidePaidOut, 2, MidpointRounding.AwayFromZero);
        var outsideToBank = inputs.OutsideCashCounted is decimal outsideCashCount
            ? decimal.Round(outsideCashCount - inputs.OutsideFloat, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(outsideCash - outsidePaidOut, 2, MidpointRounding.AwayFromZero);
        var cashToDeposit = decimal.Round(insideToBank + outsideToBank, 2, MidpointRounding.AwayFromZero);
        var totalCashVariance = insideVariance is null && outsideVariance is null
            ? (decimal?)null
            : decimal.Round((insideVariance ?? 0m) + (outsideVariance ?? 0m), 2, MidpointRounding.AwayFromZero);

        var tillReconciliations = new List<PitstopTillReconciliation>
        {
            new()
            {
                TillKey = "inside",
                TillLabel = "Inside till (ClubPOS / Terminal 0070)",
                FloatIn = inputs.InsideFloat,
                CashSales = decimal.Round(pitCash, 2, MidpointRounding.AwayFromZero),
                CashPaidOut = insidePaidOut,
                Counted = inputs.CashCounted,
                Expected = insideExpected,
                Variance = insideVariance,
                FloatKept = inputs.InsideFloat,
                CashToBank = insideToBank,
            },
            new()
            {
                TillKey = "outside",
                TillLabel = "Outside merch tin (paper sheet / Flounderers02)",
                FloatIn = inputs.OutsideFloat,
                CashSales = decimal.Round(outsideCash, 2, MidpointRounding.AwayFromZero),
                CashPaidOut = outsidePaidOut,
                Counted = inputs.OutsideCashCounted,
                Expected = outsideExpected,
                Variance = outsideVariance,
                FloatKept = inputs.OutsideFloat,
                CashToBank = outsideToBank,
            },
        };

        var gross = decimal.Round(
            pitCash + outsideCash + combinedSquare,
            2,
            MidpointRounding.AwayFromZero);
        var net = decimal.Round(gross - totalExpenses - fees, 2, MidpointRounding.AwayFromZero);

        var periodCaption =
            $"{start.LocalDateTime:dddd d MMMM yyyy} → {end.LocalDateTime:dddd d MMMM yyyy} (end exclusive)";

        var products = lines
            .Where(l => l.ItemId > 0)
            .GroupBy(l => (l.ItemId, l.ItemName, l.CategoryName))
            .Select(g => new PitstopProductAggregateRow
            {
                ItemId = g.Key.ItemId,
                Name = g.Key.ItemName,
                CategoryName = EventReportCategoryNormalizer.Normalize(g.Key.CategoryName, g.Key.ItemName),
                Quantity = g.Sum(x => x.Quantity),
                LineTotal = decimal.Round(g.Sum(x => x.LineTotal), 2, MidpointRounding.AwayFromZero),
            })
            .OrderByDescending(p => p.LineTotal)
            .ToList();

        var categories = lines
            .Where(l => l.ItemId > 0)
            .GroupBy(l => EventReportCategoryNormalizer.Normalize(l.CategoryName, l.ItemName))
            .Select(g => new PitstopCategoryAggregateRow
            {
                CategoryName = g.Key ?? string.Empty,
                Quantity = g.Sum(x => x.Quantity),
                LineTotal = decimal.Round(g.Sum(x => x.LineTotal), 2, MidpointRounding.AwayFromZero),
            })
            .OrderByDescending(c => c.LineTotal)
            .ToList();

        var outsideProducts = usingManualMode
            ? new List<PitstopProductAggregateRow>()
            : square.OutsideTerminalProductSales.ToList();
        var outsideCategories = usingManualMode
            ? new List<PitstopCategoryAggregateRow>()
            : square.OutsideTerminalCategorySales.ToList();
        var combinedOutsideSales = SquareOutsideSalesAggregator.BuildCombinedOutsideSales(
            inputs.OutsideLines,
            outsideProducts);
        var combinedProducts = SquareOutsideSalesAggregator.MergeProductSales(products, outsideProducts);
        var combinedCategories = SquareOutsideSalesAggregator.MergeCategorySales(categories, outsideCategories);
        var categoryComparison = SquareOutsideSalesAggregator.BuildCategoryComparison(
            categories,
            outsideCategories,
            combinedCategories);

        var payBreak = lines
            .Where(l => l.ItemId > 0)
            .GroupBy(l => string.IsNullOrWhiteSpace(l.PaymentMethod) ? "—" : l.PaymentMethod.Trim())
            .Select(g => new PitstopPaymentBreakdownRow
            {
                PaymentMethod = g.Key,
                Total = decimal.Round(g.Sum(x => x.LineTotal), 2, MidpointRounding.AwayFromZero),
            })
            .OrderByDescending(p => p.Total)
            .ToList();

        var warnings = inputs.Warnings.ToList();
        if (!usingManualMode)
        {
            foreach (var w in square.Warnings)
            {
                if (!string.IsNullOrWhiteSpace(w))
                {
                    warnings.Add(w);
                }
            }

            if (!string.IsNullOrWhiteSpace(square.LoadError))
            {
                warnings.Add($"Square reconciliation: {square.LoadError}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(square.LoadError)
                 && square.MatchedPayments.Count == 0
                 && !square.LoadedFromSquare)
        {
            // POS refresh failed in manual mode — still note it, but do not treat as outside confusion.
            warnings.Add($"Square POS refresh: {square.LoadError}");
        }

        if (usingManualMode)
        {
            if (usingManualFallback)
            {
                warnings.Add(
                    $"Manual Square mode — combined {combinedSquare:C2}, outside derived {outsideSquare:C2} "
                    + $"(combined minus POS card {pitCardCharged:C2}). Outside terminal not imported.");
                if (combinedSquare < pitCardCharged - MismatchTolerance)
                {
                    warnings.Add(
                        $"Manual Square total {combinedSquare:C2} is less than Pitstop terminal card {pitCardCharged:C2}.");
                }
            }
            else
            {
                warnings.Add(
                    "Manual Square mode — enter combined Square card gross for the day. Outside terminal is not imported.");
            }
        }

        return new PitstopReportData
        {
            EventName = inputs.EventName.Trim(),
            PeriodCaption = periodCaption,
            StaffName = string.IsNullOrWhiteSpace(inputs.StaffName) ? null : inputs.StaffName.Trim(),
            InsideCashFromPos = 0m,
            InsideCardFromPos = 0m,
            PitstopRetailCash = decimal.Round(pitCash, 2, MidpointRounding.AwayFromZero),
            PitstopRetailCard = decimal.Round(pitCardCharged, 2, MidpointRounding.AwayFromZero),
            PitstopCardBaseProductTotal = decimal.Round(pitCardBase, 2, MidpointRounding.AwayFromZero),
            PitstopCardSurchargeCollected = decimal.Round(pitCardSurcharge, 2, MidpointRounding.AwayFromZero),
            InsidePosCardTotalForReconciliation = decimal.Round(pitCardCharged, 2, MidpointRounding.AwayFromZero),
            CombinedSquareCardGross = combinedSquare,
            PosSquareGross = posSquare,
            OutsideSquareGross = outsideSquare,
            PosSquareTransactionCount = square.PosTransactionCount,
            OutsideSquareTransactionCount = usingManualMode ? 0 : square.OutsideTransactionCount,
            ActualSquareFees = usingManualMode ? null : square.ActualSquareFees,
            ExpectedSquareDeposit = expectedDeposit,
            SquareReconciliationLoaded = square.LoadedFromSquare,
            UsingManualSquareCardFallback = usingManualFallback,
            SquareReconciliationError = square.LoadError,
            SquareMatchedPayments = square.MatchedPayments.ToList(),
            SquareUnmatchedPayments = usingManualMode
                ? new List<SquareReconciliationPaymentRow>()
                : square.UnmatchedSquarePayments.ToList(),
            SquareMissingLocalPayments = square.MissingLocalPayments.ToList(),
            OutsideCardDerived = outsideSquare,
            OutsideCardItemisedBase = decimal.Round(pitCardCharged, 2, MidpointRounding.AwayFromZero),
            OutsideCardDifference = squareVsTerminalDiff,
            OutsideCardMismatch = squareMismatch,
            OutsideCashTotal = decimal.Round(outsideCash, 2, MidpointRounding.AwayFromZero),
            TotalCashGross = totalCash,
            TotalCardGross = combinedSquare,
            GrossSales = gross,
            TotalExpenses = totalExpenses,
            EstimatedSquareFees = fees,
            CashToDeposit = cashToDeposit,
            TotalCashVariance = totalCashVariance,
            NetEventProfit = net,
            InsideFloat = inputs.InsideFloat,
            OutsideFloat = inputs.OutsideFloat,
            SquareFeePercent = feePct,
            OutsideLines = inputs.OutsideLines.ToList(),
            CombinedOutsideSales = combinedOutsideSales,
            Expenses = inputs.Expenses.ToList(),
            PrizeGiveaways = inputs.PrizeGiveaways.ToList(),
            PitstopProductSales = products,
            PitstopCategorySales = categories,
            OutsideTerminalProductSales = outsideProducts,
            OutsideTerminalCategorySales = outsideCategories,
            CombinedEventProductSales = combinedProducts,
            CombinedEventCategorySales = combinedCategories,
            EventCategoryComparison = categoryComparison,
            PitstopPaymentBreakdown = payBreak,
            OutsideMerchRaffleCardTotal = outsideSquare,
            Warnings = warnings,
            CashCounted = inputs.CashCounted,
            FloatRemoved = inputs.FloatRemoved,
            ExpectedCash = inputs.CashCounted is null ? null : insideExpected,
            CashVariance = insideVariance,
            TillReconciliations = tillReconciliations,
            IsTestReport = inputs.UseTestPosData,
        };
    }
}
