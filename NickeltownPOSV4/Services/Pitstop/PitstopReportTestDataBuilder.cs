using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Services.Pitstop;

public static class PitstopReportTestDataBuilder
{
    public const string TestEventName = "TEST - Sample Pitstop";
    public const decimal TestInsideFloat = 200m;
    public const decimal TestOutsideFloat = 100m;
    public const decimal TestCashTotal = 487.50m;
    public const decimal TestCardBaseTotal = 900.00m;
    public const decimal TestCardSurcharge = 23.40m;
    public const decimal TestCardChargedTotal = 923.40m;

    public static PitstopRetailPeriodTotals BuildPosTotals() =>
        new()
        {
            CashTotal = TestCashTotal,
            CardChargedTotal = TestCardChargedTotal,
            CardBaseProductTotal = TestCardBaseTotal,
            CardSurchargeCollected = TestCardSurcharge,
        };

    public static IReadOnlyList<PitstopSaleLineReportRow> BuildPosLines() =>
    [
        Line("Lager", "Drinks", "Cash", 12, 8m),
        Line("Pale Ale", "Drinks", "Cash", 10, 9m),
        Line("Water", "Drinks", "Cash", 8, 4m),
        Line("Hot dog", "Food", "Cash", 15, 6.50m),
        Line("Meat pie", "Food", "Cash", 12, 7m),
        Line("Coffee", "Drinks", "Cash", 6, 4.50m),
        Line("Chips", "Food", "Cash", 9, 5.50m),
        Line("Sausage roll", "Food", "Cash", 5, 2.30m),
        Line("Burger", "Food", "Card", 15, 12m),
        Line("Pizza slice", "Food", "Card", 20, 8m),
        Line("Fish and chips", "Food", "Card", 12, 14m),
        Line("Soft drink", "Drinks", "Card", 18, 5m),
        Line("Loaded fries", "Food", "Card", 15, 8.50m),
        Line("Wine glass", "Drinks", "Card", 6, 16m),
        Line("Ice cream", "Food", "Card", 12, 6.50m),
    ];

    public static IReadOnlyList<EventExpenseRow> BuildSampleExpenses() =>
    [
        new EventExpenseRow { Description = "Ice supply", Amount = 45m },
        new EventExpenseRow { Description = "Generator fuel", Amount = 35m },
    ];

    public static void ApplyOutsideLineSamples(IList<OutsideItemSaleRow> lines)
    {
        var merchLines = lines
            .Where(l => string.Equals(l.OutsideLineKind, PitstopOutsideLineCatalogBuilder.LineKindMerchSku, StringComparison.Ordinal))
            .ToList();

        for (var i = 0; i < merchLines.Count; i++)
        {
            var line = merchLines[i];
            switch (i)
            {
                case 0:
                    line.CashQty = 5;
                    line.CardQty = 3;
                    ApplySuggestedDollars(line);
                    break;
                case 1:
                    line.CashQty = 2;
                    line.CardQty = 4;
                    ApplySuggestedDollars(line);
                    break;
                case 2:
                    line.CashQty = 0;
                    line.CardQty = 6;
                    ApplySuggestedDollars(line);
                    break;
            }
        }

        var raffle = lines.FirstOrDefault(l =>
            string.Equals(l.OutsideLineKind, PitstopOutsideLineCatalogBuilder.LineKindRaffle, StringComparison.Ordinal));
        if (raffle is not null)
        {
            raffle.CashQty = 30;
            raffle.CashDollars = 60m;
            raffle.CardQty = 15;
            raffle.CardDollars = 30m;
        }
    }

    public static PitstopEodReconciliationReport BuildReconciliationReport(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        decimal squareFeePercent)
    {
        var pit = BuildPosTotals();
        var fees = decimal.Round(pit.CardChargedTotal * (squareFeePercent / 100m), 2, MidpointRounding.AwayFromZero);
        var totalRecorded = decimal.Round(pit.CashTotal + pit.CardChargedTotal, 2, MidpointRounding.AwayFromZero);

        return new PitstopEodReconciliationReport
        {
            PeriodCaption = $"{startInclusive.LocalDateTime:dddd d MMM yyyy} -> {endExclusive.LocalDateTime:dddd d MMM yyyy}",
            CashSales = pit.CashTotal,
            CardSalesCharged = pit.CardChargedTotal,
            CardBaseProductTotal = pit.CardBaseProductTotal,
            CardSurchargeCollected = pit.CardSurchargeCollected,
            EstimatedSquareFee = fees,
            NetCardAfterFees = decimal.Round(pit.CardChargedTotal - fees, 2, MidpointRounding.AwayFromZero),
            TotalRecordedSales = totalRecorded,
            Warnings = ["TEST REPORT - sample terminal sales and figures only. Nothing was archived."],
        };
    }

    private static void ApplySuggestedDollars(OutsideItemSaleRow line)
    {
        if (line.SuggestedUnitPrice is decimal price && price > 0m)
        {
            line.CashDollars = line.CashQty <= 0 ? 0m : decimal.Round(line.CashQty * price, 2, MidpointRounding.AwayFromZero);
            line.CardDollars = line.CardQty <= 0 ? 0m : decimal.Round(line.CardQty * price, 2, MidpointRounding.AwayFromZero);
        }
    }

    private static PitstopSaleLineReportRow Line(string name, string category, string payment, int qty, decimal unitPrice) =>
        new()
        {
            ItemId = 1,
            ItemName = name,
            CategoryName = category,
            PaymentMethod = payment,
            Quantity = qty,
            UnitPrice = unitPrice,
            LineTotal = decimal.Round(qty * unitPrice, 2, MidpointRounding.AwayFromZero),
        };
}
