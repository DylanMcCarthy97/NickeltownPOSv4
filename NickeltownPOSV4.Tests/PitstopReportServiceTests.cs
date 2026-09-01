using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopReportServiceTests
{
    [Fact]
    public async Task BuildAsync_SplitsOutsideCardCash_ExcludingSurcharge()
    {
        var service = new PitstopReportService(new FakePitstopSales());
        var inputs = new PitstopReportInputs
        {
            EventName = "Pitstop",
            PeriodStartLocal = DateTimeOffset.Now.Date,
            PeriodEndLocal = DateTimeOffset.Now.Date.AddDays(1),
            CardSurchargePercent = 2.2m,
            SquareFeePercent = 1.75m,
            InsideFloat = 100m,
            OutsideFloat = 50m,
            CashCounted = 370m,
            OutsideCashCounted = 180m,
            SquareReconciliation = new SquarePaymentReconciliationResult
            {
                LoadedFromSquare = true,
                PosSquareGross = 0m,
                OutsideSquareGross = 91.98m,
                CombinedSquareGross = 91.98m,
                ExpectedSquareDeposit = 90.00m,
            },
        };
        inputs.OutsideLines.Add(new OutsideItemSaleRow
        {
            DisplayLabel = "Stubby Holder",
            SuggestedUnitPrice = 10m,
            SoldQty = 6,
            OutsideLineKind = PitstopOutsideLineCatalogBuilder.LineKindMerchSku,
        });
        inputs.OutsideLines.Add(new OutsideItemSaleRow
        {
            DisplayLabel = "Club Shirt",
            SuggestedUnitPrice = 25m,
            SoldQty = 2,
            OutsideLineKind = PitstopOutsideLineCatalogBuilder.LineKindMerchSku,
        });
        inputs.OutsideLines.Add(new OutsideItemSaleRow
        {
            DisplayLabel = "Raffle Tickets",
            SuggestedUnitPrice = 5m,
            SoldQty = 10,
            OutsideLineKind = PitstopOutsideLineCatalogBuilder.LineKindRaffle,
        });
        inputs.Expenses.Add(new EventExpenseRow
        {
            Description = "Money Wheel",
            Amount = 50m,
            Kind = EventExpenseKind.CashPrize,
            PaidFrom = EventExpensePaymentSource.InsideTill,
        });
        inputs.Expenses.Add(new EventExpenseRow
        {
            Description = "Fuel",
            Amount = 35m,
            Kind = EventExpenseKind.Expense,
            PaidFrom = EventExpensePaymentSource.Other,
        });

        var report = await service.BuildAsync(inputs);

        Assert.Equal(160.00m, report.OutsideItemSalesTotal);
        Assert.Equal(90.00m, report.OutsideCardSales);
        Assert.Equal(1.98m, report.OutsideCardSurchargeCollected);
        Assert.Equal(70.00m, report.OutsideCashTotal);
        Assert.Equal(320.00m, report.PitstopRetailCash);
        Assert.Equal(370.00m, report.TillReconciliations[0].Expected);
        Assert.Equal(270.00m, report.TillReconciliations[0].CashToBank);
        Assert.Equal(130.00m, report.TillReconciliations[1].CashToBank);
        Assert.Equal(400.00m, report.CashToDeposit);
        Assert.Equal(50.00m, report.TotalCashPrizes);
        Assert.Equal(35.00m, report.TotalExpenses);
        Assert.Equal(880.00m, report.GrossSales);
        Assert.Equal(PitstopEodCalculator.EstimatedProfit(report.GrossSales, 35m, 50m, 0m, report.EstimatedSquareFees), report.NetEventProfit);
    }

    [Fact]
    public async Task BuildAsync_ManualSquareFallback_UsesEnteredGross()
    {
        var service = new PitstopReportService(new FakePitstopSales(cash: 0m, card: 0m));
        var inputs = new PitstopReportInputs
        {
            EventName = "Pitstop",
            PeriodStartLocal = DateTimeOffset.Now.Date,
            PeriodEndLocal = DateTimeOffset.Now.Date.AddDays(1),
            UseManualSquareCardMode = true,
            ManualCombinedSquareCardGross = 91.98m,
            CardSurchargePercent = 2.2m,
            SquareReconciliation = SquarePaymentReconciliationResult.Empty("Square unavailable"),
        };
        inputs.OutsideLines.Add(new OutsideItemSaleRow
        {
            DisplayLabel = "Club Shirt",
            SuggestedUnitPrice = 25m,
            SoldQty = 4,
            OutsideLineKind = PitstopOutsideLineCatalogBuilder.LineKindMerchSku,
        });

        var report = await service.BuildAsync(inputs);

        Assert.True(report.UsingManualSquareCardFallback);
        Assert.Equal(90.00m, report.OutsideCardSales);
        Assert.Equal(10.00m, report.OutsideCashTotal);
    }

    [Fact]
    public async Task BuildAsync_UnknownStockCost_DoesNotTreatMissingAsZero()
    {
        var service = new PitstopReportService(new FakePitstopSales());
        var inputs = new PitstopReportInputs
        {
            EventName = "Pitstop",
            PeriodStartLocal = DateTimeOffset.Now.Date,
            PeriodEndLocal = DateTimeOffset.Now.Date.AddDays(1),
            UseTestPosData = true,
        };
        inputs.ItemUnitCosts[1] = 2.00m;
        inputs.PrizeGiveaways.Add(new MerchPrizeGiveawayRow { ItemId = 99, ItemName = "Old merch", Quantity = 1 });

        var report = await service.BuildAsync(inputs);

        Assert.True(report.HasUnknownStockCosts);
        Assert.True(report.KnownStockCosts > 0m);
    }

    private sealed class FakePitstopSales : IPitstopRetailSaleRepository
    {
        private readonly decimal _cash;
        private readonly decimal _card;

        public FakePitstopSales(decimal cash = 320m, decimal card = 400m)
        {
            _cash = cash;
            _card = card;
        }
        public Task<PitstopSaleCommitResult> CommitSaleAsync(
            IReadOnlyList<PitstopSaleLineCommit> lines,
            PitstopSalePaymentCommit payment,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PitstopSaleLineReportRow>> GetItemisedLinesAsync(
            DateTimeOffset startInclusive,
            DateTimeOffset endExclusive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PitstopSaleLineReportRow>>([]);

        public Task<PitstopRetailPeriodTotals> GetPitstopRetailPaymentTotalsAsync(
            DateTimeOffset startInclusive,
            DateTimeOffset endExclusive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PitstopRetailPeriodTotals
            {
                CashTotal = _cash,
                CardChargedTotal = _card,
                CardBaseProductTotal = _card,
                CardSurchargeCollected = 0m,
            });

        public Task<IReadOnlyList<PitstopCardSaleRefRow>> GetPitstopCardSalesForPeriodAsync(
            DateTimeOffset startInclusive,
            DateTimeOffset endExclusive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PitstopCardSaleRefRow>>([]);

        public Task<PitstopDaySalesClearResult> ClearPitstopRetailSalesForPeriodAsync(
            DateTimeOffset startInclusive,
            DateTimeOffset endExclusive,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PitstopActiveSaleRow>> GetActivePitstopSalesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PitstopActiveSaleRow>>([]);

        public Task<int> GetVoidedPitstopSaleCountForPeriodAsync(
            DateTimeOffset startInclusive,
            DateTimeOffset endExclusive,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<PitstopVoidSaleResult> VoidPitstopSaleAsync(
            PitstopVoidSaleRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
