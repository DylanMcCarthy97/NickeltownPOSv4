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

        var square = inputs.SquareReconciliation ?? SquarePaymentReconciliationResult.Empty("Square reconciliation has not been loaded.");
        var manualCombined = inputs.ManualCombinedSquareCardGross;
        var usingManualMode = inputs.UseManualSquareCardMode;
        var usingManualFallback = usingManualMode && manualCombined is > 0m;

        decimal combinedSquare;
        decimal posSquare;
        decimal outsideSquare;
        if (usingManualMode)
        {
            posSquare = PitstopEodCalculator.RoundMoney(pitCardCharged);
            if (usingManualFallback)
            {
                combinedSquare = PitstopEodCalculator.RoundMoney(manualCombined!.Value);
                outsideSquare = PitstopEodCalculator.RoundMoney(Math.Max(0m, combinedSquare - pitCardCharged));
            }
            else
            {
                combinedSquare = posSquare;
                outsideSquare = 0m;
            }
        }
        else
        {
            combinedSquare = PitstopEodCalculator.RoundMoney(square.CombinedSquareGross);
            posSquare = PitstopEodCalculator.RoundMoney(square.PosSquareGross);
            outsideSquare = PitstopEodCalculator.RoundMoney(square.OutsideSquareGross);
        }

        var outsideItemSales = PitstopEodCalculator.OutsideItemSalesTotal(inputs.OutsideLines);
        var (outsideCardSales, outsideCardSurcharge) = PitstopEodCalculator.ActualOutsideCardSales(
            outsideSquare,
            inputs.CardSurchargePercent);
        var outsideCash = PitstopEodCalculator.OutsideCashSales(outsideItemSales, outsideCardSales);
        var outsideTenderMismatch = PitstopEodCalculator.IsOutsideTenderMismatch(outsideItemSales, outsideCardSales);

        var squareVsTerminalDiff = PitstopEodCalculator.RoundMoney(combinedSquare - pitCardCharged);
        var squareMismatch = usingManualFallback
            ? combinedSquare < pitCardCharged - PitstopEodCalculator.MismatchTolerance
            : !usingManualMode && Math.Abs(posSquare - pitCardCharged) > PitstopEodCalculator.MismatchTolerance;

        var feePct = inputs.SquareFeePercent;
        var fees = !usingManualMode && square.ActualSquareFees is decimal actualFees
            ? PitstopEodCalculator.RoundMoney(actualFees)
            : PitstopEodCalculator.RoundMoney(combinedSquare * (feePct / 100m));
        var expectedDeposit = !usingManualMode && square.LoadedFromSquare
            ? PitstopEodCalculator.RoundMoney(square.ExpectedSquareDeposit)
            : PitstopEodCalculator.RoundMoney(combinedSquare - fees);

        var totalCashPrizes = PitstopEodCalculator.TotalCashPrizes(inputs.Expenses);
        var totalExpenses = PitstopEodCalculator.TotalExpenses(inputs.Expenses);
        var insidePaidOut = PitstopEodCalculator.CashPaidOut(inputs.Expenses, EventExpensePaymentSource.InsideTill);
        var outsidePaidOut = PitstopEodCalculator.CashPaidOut(inputs.Expenses, EventExpensePaymentSource.OutsideTin);

        var insideExpected = PitstopEodCalculator.ExpectedCash(inputs.InsideFloat, pitCash, insidePaidOut);
        var outsideExpected = PitstopEodCalculator.ExpectedCash(inputs.OutsideFloat, outsideCash, outsidePaidOut);
        var insideVariance = PitstopEodCalculator.CashVariance(inputs.CashCounted, insideExpected);
        var outsideVariance = PitstopEodCalculator.CashVariance(inputs.OutsideCashCounted, outsideExpected);

        var insideToBank = PitstopEodCalculator.TillCashToBank(
            inputs.CashCounted, inputs.InsideFloat, pitCash, insidePaidOut);
        var outsideToBank = PitstopEodCalculator.TillCashToBank(
            inputs.OutsideCashCounted, inputs.OutsideFloat, outsideCash, outsidePaidOut);
        var cashToDeposit = PitstopEodCalculator.RoundMoney(insideToBank + outsideToBank);
        var totalCashVariance = insideVariance is null && outsideVariance is null
            ? (decimal?)null
            : PitstopEodCalculator.RoundMoney((insideVariance ?? 0m) + (outsideVariance ?? 0m));

        var tillReconciliations = new List<PitstopTillReconciliation>
        {
            new()
            {
                TillKey = "inside",
                TillLabel = "Inside till (ClubPOS / Terminal 0070)",
                FloatIn = inputs.InsideFloat,
                CashSales = PitstopEodCalculator.RoundMoney(pitCash),
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
                CashSales = PitstopEodCalculator.RoundMoney(outsideCash),
                CashPaidOut = outsidePaidOut,
                Counted = inputs.OutsideCashCounted,
                Expected = outsideExpected,
                Variance = outsideVariance,
                FloatKept = inputs.OutsideFloat,
                CashToBank = outsideToBank,
            },
        };

        var totalCash = PitstopEodCalculator.TotalCashSales(pitCash, outsideCash);
        var totalCardProduct = PitstopEodCalculator.TotalCardSales(pitCardBase, outsideCardSales);
        var gross = PitstopEodCalculator.TotalSales(pitCash, pitCardBase, outsideItemSales);
        var stockCostLines = BuildStockCostLines(inputs, lines);
        var (knownStockCosts, hasUnknownStockCosts) = PitstopEodCalculator.KnownStockCosts(stockCostLines);
        var net = PitstopEodCalculator.EstimatedProfit(
            gross,
            totalExpenses,
            totalCashPrizes,
            knownStockCosts,
            fees);

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
                if (combinedSquare < pitCardCharged - PitstopEodCalculator.MismatchTolerance)
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

        if (outsideTenderMismatch)
        {
            warnings.Add(
                $"Outside card sales {outsideCardSales:C2} are higher than outside item sales {outsideItemSales:C2}. Check quantities or the card total.");
        }

        if (outsideCardSurcharge > 0m)
        {
            warnings.Add(
                $"Outside Square received {outsideSquare:C2} includes {outsideCardSurcharge:C2} customer card surcharge, which is not counted as merchandise sales.");
        }

        return new PitstopReportData
        {
            EventName = inputs.EventName.Trim(),
            PeriodCaption = periodCaption,
            StaffName = string.IsNullOrWhiteSpace(inputs.StaffName) ? null : inputs.StaffName.Trim(),
            InsideCashFromPos = 0m,
            InsideCardFromPos = 0m,
            PitstopRetailCash = PitstopEodCalculator.RoundMoney(pitCash),
            PitstopRetailCard = PitstopEodCalculator.RoundMoney(pitCardCharged),
            PitstopCardBaseProductTotal = PitstopEodCalculator.RoundMoney(pitCardBase),
            PitstopCardSurchargeCollected = PitstopEodCalculator.RoundMoney(pitCardSurcharge),
            InsidePosCardTotalForReconciliation = PitstopEodCalculator.RoundMoney(pitCardCharged),
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
            OutsideCardDerived = outsideCardSales,
            OutsideCardItemisedBase = PitstopEodCalculator.RoundMoney(pitCardCharged),
            OutsideCardDifference = squareVsTerminalDiff,
            OutsideCardMismatch = squareMismatch || outsideTenderMismatch,
            OutsideCashTotal = PitstopEodCalculator.RoundMoney(outsideCash),
            OutsideItemSalesTotal = outsideItemSales,
            OutsideCardSales = outsideCardSales,
            OutsideCardSurchargeCollected = outsideCardSurcharge,
            TotalCashGross = totalCash,
            TotalCardGross = totalCardProduct,
            GrossSales = gross,
            TotalExpenses = totalExpenses,
            TotalCashPrizes = totalCashPrizes,
            KnownStockCosts = knownStockCosts,
            HasUnknownStockCosts = hasUnknownStockCosts,
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

    private static IEnumerable<StockCostLine> BuildStockCostLines(
        PitstopReportInputs inputs,
        IReadOnlyList<PitstopSaleLineReportRow> posLines)
    {
        foreach (var line in posLines.Where(l => l.ItemId > 0 && l.Quantity > 0))
        {
            yield return new StockCostLine(line.Quantity, LookupCost(inputs, line.ItemId));
        }

        foreach (var line in inputs.OutsideLines)
        {
            if (line.PitstopItemId is not > 0)
            {
                continue;
            }

            var qty = PitstopEodCalculator.SoldQuantity(line);
            if (qty <= 0)
            {
                continue;
            }

            yield return new StockCostLine(qty, LookupCost(inputs, line.PitstopItemId.Value));
        }

        foreach (var prize in inputs.PrizeGiveaways.Where(p => p.Quantity > 0 && p.ItemId > 0))
        {
            yield return new StockCostLine(prize.Quantity, LookupCost(inputs, prize.ItemId));
        }
    }

    private static decimal? LookupCost(PitstopReportInputs inputs, long itemId)
    {
        if (itemId <= 0)
        {
            return null;
        }

        return inputs.ItemUnitCosts.TryGetValue(itemId, out var cost) ? cost : null;
    }
}
