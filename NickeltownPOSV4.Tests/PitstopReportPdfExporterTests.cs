using iTextSharp.text.pdf;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopReportPdfExporterTests
{
    [Fact]
    public void Build_ProducesPdfWithSalesPrizesAndCloseDetails()
    {
        var data = new PitstopReportData
        {
            EventName = "Sunday Pitstop",
            PeriodCaption = "Sunday 16 August 2026",
            StaffName = "Dylan",
            PitstopRetailCash = 320m,
            PitstopCardBaseProductTotal = 400m,
            PitstopRetailCard = 408.80m,
            PitstopCardSurchargeCollected = 8.80m,
            OutsideItemSalesTotal = 160m,
            OutsideCashTotal = 70m,
            OutsideCardSales = 90m,
            OutsideCardSurchargeCollected = 1.98m,
            OutsideSquareGross = 91.98m,
            CombinedSquareCardGross = 500.78m,
            EstimatedSquareFees = 8.76m,
            ExpectedSquareDeposit = 492.02m,
            TotalCashGross = 390m,
            TotalCardGross = 490m,
            GrossSales = 880m,
            TotalExpenses = 35m,
            TotalCashPrizes = 50m,
            KnownStockCosts = 12m,
            HasUnknownStockCosts = true,
            NetEventProfit = 774.24m,
            CashToDeposit = 400m,
            InsideFloat = 100m,
            OutsideFloat = 50m,
            SquareFeePercent = 1.75m,
            PitstopProductSales =
            [
                new PitstopProductAggregateRow { Name = "Lager", Quantity = 12, LineTotal = 96m },
            ],
            CombinedOutsideSales =
            [
                new CombinedOutsideSaleRow { Name = "Club Shirt", CashQuantity = 2, CashTotal = 50m },
            ],
            Expenses =
            [
                new EventExpenseRow
                {
                    Description = "Money Wheel",
                    Amount = 50m,
                    Kind = EventExpenseKind.CashPrize,
                    PaidFrom = EventExpensePaymentSource.InsideTill,
                },
                new EventExpenseRow
                {
                    Description = "Generator fuel",
                    Amount = 35m,
                    Kind = EventExpenseKind.Expense,
                    PaidFrom = EventExpensePaymentSource.Other,
                },
            ],
            PrizeGiveaways =
            [
                new MerchPrizeGiveawayRow { ItemId = 9, ItemName = "Stubby Holder", Quantity = 1 },
            ],
            TillReconciliations =
            [
                new PitstopTillReconciliation
                {
                    TillKey = "inside",
                    TillLabel = "Inside till",
                    FloatIn = 100m,
                    CashSales = 320m,
                    CashPaidOut = 50m,
                    Counted = 370m,
                    Expected = 370m,
                    Variance = 0m,
                    FloatKept = 100m,
                    CashToBank = 270m,
                },
                new PitstopTillReconciliation
                {
                    TillKey = "outside",
                    TillLabel = "Outside tin",
                    FloatIn = 50m,
                    CashSales = 70m,
                    Counted = 120m,
                    Expected = 120m,
                    Variance = 0m,
                    FloatKept = 50m,
                    CashToBank = 70m,
                },
            ],
        };

        var pdf = PitstopReportPdfExporter.Build(data);
        Assert.True(pdf.Length > 500);
        using var reader = new PdfReader(pdf);
        Assert.True(reader.NumberOfPages >= 1);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}