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
        var combinedSquare = decimal.Round(square.CombinedSquareGross, 2, MidpointRounding.AwayFromZero);
        var posSquare = decimal.Round(square.PosSquareGross, 2, MidpointRounding.AwayFromZero);
        var outsideSquare = decimal.Round(square.OutsideSquareGross, 2, MidpointRounding.AwayFromZero);
        var squareVsTerminalDiff = decimal.Round(posSquare - pitCardCharged, 2, MidpointRounding.AwayFromZero);
        var squareMismatch = Math.Abs(squareVsTerminalDiff) > MismatchTolerance || square.Warnings.Count > 0;

        var feePct = inputs.SquareFeePercent;
        var fees = square.ActualSquareFees is decimal actualFees
            ? decimal.Round(actualFees, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(combinedSquare * (feePct / 100m), 2, MidpointRounding.AwayFromZero);
        var expectedDeposit = square.LoadedFromSquare
            ? decimal.Round(square.ExpectedSquareDeposit, 2, MidpointRounding.AwayFromZero)
            : decimal.Round(combinedSquare - fees, 2, MidpointRounding.AwayFromZero);

        var cashFloat = decimal.Round(inputs.InsideFloat + inputs.OutsideFloat, 2, MidpointRounding.AwayFromZero);
        var totalCash = decimal.Round(pitCash + outsideCash, 2, MidpointRounding.AwayFromZero);
        var cashToDeposit = decimal.Round(totalCash - cashFloat, 2, MidpointRounding.AwayFromZero);

        var totalExpenses = decimal.Round(inputs.Expenses.Sum(e => e.Amount), 2, MidpointRounding.AwayFromZero);

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

        var outsideProducts = square.OutsideTerminalProductSales.ToList();
        var outsideCategories = square.OutsideTerminalCategorySales.ToList();
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
            OutsideSquareTransactionCount = square.OutsideTransactionCount,
            ActualSquareFees = square.ActualSquareFees,
            ExpectedSquareDeposit = expectedDeposit,
            SquareReconciliationLoaded = square.LoadedFromSquare,
            SquareReconciliationError = square.LoadError,
            SquareMatchedPayments = square.MatchedPayments.ToList(),
            SquareUnmatchedPayments = square.UnmatchedSquarePayments.ToList(),
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
            ExpectedCash = inputs.CashCounted is null
                ? null
                : decimal.Round(inputs.InsideFloat + pitCash, 2, MidpointRounding.AwayFromZero),
            CashVariance = inputs.CashCounted is decimal cc
                ? cc - decimal.Round(inputs.InsideFloat + pitCash, 2, MidpointRounding.AwayFromZero)
                : (decimal?)null,
            IsTestReport = inputs.UseTestPosData,
        };
    }
}
