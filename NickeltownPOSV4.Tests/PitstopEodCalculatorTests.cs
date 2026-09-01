using System.Collections.Generic;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopEodCalculatorTests
{
    [Fact]
    public void OutsideItemSales_UsesQuantityTimesSellingPrice()
    {
        var lines = new List<OutsideItemSaleRow>
        {
            new() { DisplayLabel = "Stubby Holder", SuggestedUnitPrice = 10m, SoldQty = 6 },
            new() { DisplayLabel = "Club Shirt", SuggestedUnitPrice = 25m, SoldQty = 2 },
            new() { DisplayLabel = "Raffle Tickets", SuggestedUnitPrice = 5m, SoldQty = 10 },
        };

        Assert.Equal(160.00m, PitstopEodCalculator.OutsideItemSalesTotal(lines));
    }

    [Fact]
    public void OutsideCardAndCash_ExcludesCustomerSurchargeFromSales()
    {
        var itemSales = 160.00m;
        var squareGross = 91.98m;
        var surchargePercent = 2.2m;

        var (cardSales, surcharge) = PitstopEodCalculator.ActualOutsideCardSales(squareGross, surchargePercent);
        var cashSales = PitstopEodCalculator.OutsideCashSales(itemSales, cardSales);

        Assert.Equal(90.00m, cardSales);
        Assert.Equal(1.98m, surcharge);
        Assert.Equal(70.00m, cashSales);
        Assert.False(PitstopEodCalculator.IsOutsideTenderMismatch(itemSales, cardSales));
    }

    [Fact]
    public void OutsideCard_WhenSurchargeDisabled_EqualsSquareGross()
    {
        var (cardSales, surcharge) = PitstopEodCalculator.ActualOutsideCardSales(91.98m, 0m);

        Assert.Equal(91.98m, cardSales);
        Assert.Equal(0m, surcharge);
        Assert.Equal(68.02m, PitstopEodCalculator.OutsideCashSales(160m, cardSales));
    }

    [Fact]
    public void OutsideCash_FlagsWhenCardExceedsItemSales()
    {
        Assert.True(PitstopEodCalculator.IsOutsideTenderMismatch(80m, 90m));
        Assert.Equal(-10.00m, PitstopEodCalculator.OutsideCashSales(80m, 90m));
    }

    [Fact]
    public void CashPrize_ReducesExpectedCash_AndProfit()
    {
        var prizes = new List<EventExpenseRow>
        {
            new()
            {
                Description = "Money Wheel",
                Amount = 50m,
                Kind = EventExpenseKind.CashPrize,
                PaidFrom = EventExpensePaymentSource.InsideTill,
            },
        };

        var expected = PitstopEodCalculator.ExpectedCash(100m, 320m, PitstopEodCalculator.CashPaidOut(prizes, EventExpensePaymentSource.InsideTill));
        Assert.Equal(370.00m, expected);

        var profit = PitstopEodCalculator.EstimatedProfit(
            totalSales: 500m,
            expenses: 0m,
            cashPrizes: PitstopEodCalculator.TotalCashPrizes(prizes),
            knownStockCosts: 0m,
            squareProcessingFees: 0m);
        Assert.Equal(450.00m, profit);
    }

    [Fact]
    public void BankPaidExpense_ReducesProfit_ButNotExpectedTillCash()
    {
        var expenses = new List<EventExpenseRow>
        {
            new()
            {
                Description = "Generator fuel",
                Amount = 35m,
                Kind = EventExpenseKind.Expense,
                PaidFrom = EventExpensePaymentSource.Other,
            },
        };

        Assert.Equal(0m, PitstopEodCalculator.CashPaidOut(expenses, EventExpensePaymentSource.InsideTill));
        Assert.Equal(0m, PitstopEodCalculator.CashPaidOut(expenses, EventExpensePaymentSource.OutsideTin));
        Assert.Equal(420.00m, PitstopEodCalculator.ExpectedCash(100m, 320m, 0m));
        Assert.Equal(35.00m, PitstopEodCalculator.TotalExpenses(expenses));
        Assert.Equal(0m, PitstopEodCalculator.TotalCashPrizes(expenses));

        var profit = PitstopEodCalculator.EstimatedProfit(500m, 35m, 0m, 0m, 0m);
        Assert.Equal(465.00m, profit);
    }

    [Fact]
    public void TillPaidExpense_ReducesExpectedCash_AndIsRetainedUnderAdvancedPath()
    {
        var expenses = new List<EventExpenseRow>
        {
            new()
            {
                Description = "Ice from till",
                Amount = 12m,
                Kind = EventExpenseKind.Expense,
                PaidFrom = EventExpensePaymentSource.OutsideTin,
            },
        };

        Assert.Equal(12.00m, PitstopEodCalculator.CashPaidOut(expenses, EventExpensePaymentSource.OutsideTin));
        Assert.Equal(218.00m, PitstopEodCalculator.ExpectedCash(50m, 180m, 12m));
        Assert.Equal(12.00m, PitstopEodCalculator.TotalExpenses(expenses));
    }

    [Fact]
    public void Floats_AffectCashToBank_ButNotProfit()
    {
        var insideExpected = PitstopEodCalculator.ExpectedCash(100m, 320m, 50m);
        var outsideExpected = PitstopEodCalculator.ExpectedCash(50m, 180m, 50m);
        Assert.Equal(370.00m, insideExpected);
        Assert.Equal(180.00m, outsideExpected);

        var insideToBank = PitstopEodCalculator.TillCashToBank(370m, 100m, 320m, 50m);
        var outsideToBank = PitstopEodCalculator.TillCashToBank(180m, 50m, 180m, 50m);
        Assert.Equal(270.00m, insideToBank);
        Assert.Equal(130.00m, outsideToBank);
        Assert.Equal(400.00m, insideToBank + outsideToBank);

        var profit = PitstopEodCalculator.EstimatedProfit(
            totalSales: 500m,
            expenses: 0m,
            cashPrizes: 100m,
            knownStockCosts: 0m,
            squareProcessingFees: 0m);
        Assert.Equal(400.00m, profit);
    }

    [Fact]
    public void KnownAndUnknownStockCosts_DoNotTreatMissingCostAsZero()
    {
        var (known, hasUnknown) = PitstopEodCalculator.KnownStockCosts(
        [
            new StockCostLine(2, 4.50m),
            new StockCostLine(1, null),
            new StockCostLine(3, 0m),
        ]);

        Assert.Equal(9.00m, known);
        Assert.True(hasUnknown);

        var complete = PitstopEodCalculator.KnownStockCosts([new StockCostLine(2, 4.50m)]);
        Assert.Equal(9.00m, complete.knownCosts);
        Assert.False(complete.hasUnknown);
    }

    [Fact]
    public void EstimatedProfit_UsesKnownCostsAndFees_NotFloats()
    {
        var profit = PitstopEodCalculator.EstimatedProfit(
            totalSales: 1000m,
            expenses: 35m,
            cashPrizes: 50m,
            knownStockCosts: 120m,
            squareProcessingFees: 18.50m);

        Assert.Equal(776.50m, profit);
    }

    [Fact]
    public void VarianceStatus_BalancedShortAndOver()
    {
        Assert.Equal("BALANCED", PitstopEodCalculator.VarianceStatus(0m));
        Assert.Equal("5.00 SHORT", PitstopEodCalculator.VarianceStatus(-5m));
        Assert.Equal("5.00 OVER", PitstopEodCalculator.VarianceStatus(5m));
        Assert.Equal(string.Empty, PitstopEodCalculator.VarianceStatus(null));
    }

    [Fact]
    public void DuplicateFinalisation_IsBlockedWhenAlreadyArchivedThisSessionOrPeriod()
    {
        Assert.True(PitstopEodCalculator.IsDuplicateFinalisation(alreadyArchivedThisSession: true, periodAlreadyArchived: false));
        Assert.True(PitstopEodCalculator.IsDuplicateFinalisation(alreadyArchivedThisSession: false, periodAlreadyArchived: true));
        Assert.False(PitstopEodCalculator.IsDuplicateFinalisation(alreadyArchivedThisSession: false, periodAlreadyArchived: false));
    }

    [Fact]
    public void CloseRetry_ResumesFromPartialFailureWithoutRepeatingCompletedWork()
    {
        var state = new PitstopEodCloseState { PdfSaved = true, PdfPath = @"C:\tmp\eod.pdf" };
        Assert.Equal(PitstopEodCloseCheckpoint.Archive, state.Next);

        state.Archived = true;
        state.BatchId = 12;
        Assert.Equal(PitstopEodCloseCheckpoint.ApplyStock, state.Next);

        state.StockApplied = true;
        Assert.Equal(PitstopEodCloseCheckpoint.Complete, state.Next);
        Assert.True(state.IsComplete);
    }

    [Fact]
    public void ManualSquareFallback_UsesEnteredGrossAsOutsideReceived()
    {
        var (cardSales, surcharge) = PitstopEodCalculator.ActualOutsideCardSales(91.98m, 2.2m);
        Assert.Equal(90.00m, cardSales);
        Assert.Equal(1.98m, surcharge);
    }
}

public sealed class SquareCardFeeCalculatorSurchargeSplitTests
{
    [Fact]
    public void SplitGrossExcludingSurcharge_RemovesConfiguredSurcharge()
    {
        var (product, surcharge) = SquareCardFeeCalculator.SplitGrossExcludingSurcharge(91.98m, 2.2m);
        Assert.Equal(90.00m, product);
        Assert.Equal(1.98m, surcharge);
    }

    [Fact]
    public void SplitGrossExcludingSurcharge_ZeroPercentLeavesGrossUnchanged()
    {
        var (product, surcharge) = SquareCardFeeCalculator.SplitGrossExcludingSurcharge(91.98m, 0m);
        Assert.Equal(91.98m, product);
        Assert.Equal(0m, surcharge);
    }
}
