using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Reconciliation;

namespace NickeltownPOSV4.Services.Reconciliation;

public sealed class EodReconciliationService
{
    private readonly IInsideBarSalesSummaryQuery _barSummary;
    private readonly IPitstopRetailSaleRepository _pitstopSales;

    public EodReconciliationService(
        IInsideBarSalesSummaryQuery barSummary,
        IPitstopRetailSaleRepository pitstopSales)
    {
        _barSummary = barSummary;
        _pitstopSales = pitstopSales;
    }

    public async Task<EodReconciliationReport> BuildAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        decimal squareFeePercent,
        CancellationToken cancellationToken = default)
    {
        var bar = await _barSummary.GetSummaryAsync(startInclusive, endExclusive, cancellationToken).ConfigureAwait(false);
        var pit = await _pitstopSales.GetPitstopRetailPaymentTotalsAsync(startInclusive, endExclusive, cancellationToken).ConfigureAwait(false);
        var barSquare = bar.SquarePayments;
        var pitCard = pit.CardChargedTotal;
        var totalCard = decimal.Round(barSquare + pitCard, 2, MidpointRounding.AwayFromZero);
        var fees = decimal.Round(totalCard * (squareFeePercent / 100m), 2, MidpointRounding.AwayFromZero);
        return new EodReconciliationReport
        {
            PeriodCaption = startInclusive.LocalDateTime.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
            BarTabCashPayments = bar.CashTopUps,
            BarTabSquarePayments = barSquare,
            PitstopCashSales = pit.CashTotal,
            PitstopCardSales = pitCard,
            TotalCardSurchargeCollected = pit.CardSurchargeCollected,
            EstimatedSquareFee = fees,
            NetCardAfterFees = decimal.Round(totalCard - fees, 2, MidpointRounding.AwayFromZero),
            TotalCashExpected = decimal.Round(bar.CashTopUps + pit.CashTotal, 2, MidpointRounding.AwayFromZero),
            TotalPosRecordedSales = decimal.Round(bar.CashTopUps + barSquare + pit.CashTotal + pitCard, 2, MidpointRounding.AwayFromZero),
            SquareReferenceLines = Array.Empty<EodReconciliationLine>(),
            Warnings = Array.Empty<string>(),
        };
    }
}