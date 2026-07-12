using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Settings;
using Square;
using Square.Payments;

namespace NickeltownPOSV4.Services.Pitstop;

public sealed class SquarePaymentReconciliationService : ISquarePaymentReconciliationService
{
    private static readonly HashSet<string> CompletedStatuses = new(StringComparer.OrdinalIgnoreCase) { "COMPLETED" };

    private readonly ISquareConfigService _config;
    private readonly IPitstopRetailSaleRepository _pitstopSales;
    private readonly SquareOutsideOrderEnrichment _outsideOrderEnrichment;

    public SquarePaymentReconciliationService(
        ISquareConfigService config,
        IPitstopRetailSaleRepository pitstopSales,
        SquareOutsideOrderEnrichment outsideOrderEnrichment)
    {
        _config = config;
        _pitstopSales = pitstopSales;
        _outsideOrderEnrichment = outsideOrderEnrichment;
    }

    public async Task<SquarePaymentReconciliationResult> ReconcileAsync(
        DateTimeOffset periodStartLocal,
        DateTimeOffset periodEndLocal,
        decimal squareFeePercentFallback,
        CancellationToken cancellationToken = default)
    {
        var end = periodEndLocal;
        if (end <= periodStartLocal)
        {
            end = periodStartLocal.AddDays(1);
        }

        var localSales = await _pitstopSales
            .GetPitstopCardSalesForPeriodAsync(periodStartLocal, end, cancellationToken)
            .ConfigureAwait(false);

        var pitTotals = await _pitstopSales
            .GetPitstopRetailPaymentTotalsAsync(periodStartLocal, end, cancellationToken)
            .ConfigureAwait(false);

        var cfg = await _config.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(cfg.AccessToken))
        {
            return ApplyLoadFailure(
                SquarePaymentReconciliationMatcher.Match(
                    Array.Empty<SquarePaymentReconciliationMatcher.SquarePaymentSnapshot>(),
                    localSales,
                    pitTotals.CardChargedTotal,
                    squareFeePercentFallback),
                "Square is not configured (access token missing).");
        }

        FetchSquarePaymentsResult fetchResult;
        try
        {
            fetchResult = await FetchSquarePaymentsAsync(cfg, periodStartLocal, end, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return ApplyLoadFailure(
                SquarePaymentReconciliationMatcher.Match(
                    Array.Empty<SquarePaymentReconciliationMatcher.SquarePaymentSnapshot>(),
                    localSales,
                    pitTotals.CardChargedTotal,
                    squareFeePercentFallback),
                ex.Message);
        }

        var result = SquarePaymentReconciliationMatcher.Match(
            fetchResult.Snapshots,
            localSales,
            pitTotals.CardChargedTotal,
            squareFeePercentFallback);

        if (result.UnmatchedSquarePayments.Count == 0)
        {
            return result;
        }

        return await _outsideOrderEnrichment
            .EnrichAsync(cfg, result, fetchResult.PaymentOrderIds, cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed class FetchSquarePaymentsResult
    {
        public List<SquarePaymentReconciliationMatcher.SquarePaymentSnapshot> Snapshots { get; init; } = new();

        public Dictionary<string, string> PaymentOrderIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<FetchSquarePaymentsResult> FetchSquarePaymentsAsync(
        AppSquareConfig cfg,
        DateTimeOffset periodStartLocal,
        DateTimeOffset periodEndLocal,
        CancellationToken cancellationToken)
    {
        var isSandbox = string.Equals(cfg.Environment?.Trim(), "sandbox", StringComparison.OrdinalIgnoreCase);
        var baseUrl = isSandbox ? SquareEnvironment.Sandbox : SquareEnvironment.Production;
        var client = new SquareClient(cfg.AccessToken.Trim(), new ClientOptions { BaseUrl = baseUrl });

        var request = new ListPaymentsRequest
        {
            BeginTime = periodStartLocal.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            EndTime = periodEndLocal.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            SortField = ListPaymentsRequestSortField.CreatedAt,
            Limit = 100,
        };

        if (!string.IsNullOrWhiteSpace(cfg.LocationId))
        {
            request.LocationId = cfg.LocationId.Trim();
        }

        var pager = await client.Payments.ListAsync(request, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var fetchResult = new FetchSquarePaymentsResult();
        await foreach (var page in pager.AsPagesAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var payment in page.Items)
            {
                if (payment is null || string.IsNullOrWhiteSpace(payment.Id))
                {
                    continue;
                }

                if (!CompletedStatuses.Contains(payment.Status ?? string.Empty))
                {
                    continue;
                }

                var gross = MoneyToDecimal(payment.TotalMoney) ?? MoneyToDecimal(payment.AmountMoney);
                if (gross is not decimal amount || amount <= 0m)
                {
                    continue;
                }

                var paymentId = payment.Id.Trim();
                if (!string.IsNullOrWhiteSpace(payment.OrderId))
                {
                    fetchResult.PaymentOrderIds[paymentId] = payment.OrderId.Trim();
                }

                fetchResult.Snapshots.Add(new SquarePaymentReconciliationMatcher.SquarePaymentSnapshot
                {
                    PaymentId = paymentId,
                    PaidAt = ParseSquareTimestamp(payment.CreatedAt) ?? periodStartLocal,
                    GrossAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero),
                    ReceiptNumber = payment.ReceiptNumber,
                    DeviceName = payment.DeviceDetails?.DeviceName,
                    CardLast4 = payment.CardDetails?.Card?.Last4,
                    ProcessingFees = SumProcessingFees(payment.ProcessingFee),
                });
            }
        }

        return fetchResult;
    }

    private static decimal SumProcessingFees(IEnumerable<ProcessingFee>? fees)
    {
        if (fees is null)
        {
            return 0m;
        }

        decimal total = 0m;
        foreach (var fee in fees)
        {
            var amount = MoneyToDecimal(fee.AmountMoney);
            if (amount is decimal v)
            {
                total += Math.Abs(v);
            }
        }

        return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal? MoneyToDecimal(Money? money)
    {
        if (money?.Amount is not long cents)
        {
            return null;
        }

        return decimal.Round(cents / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static DateTimeOffset? ParseSquareTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto
            : null;
    }

    private static SquarePaymentReconciliationResult ApplyLoadFailure(
        SquarePaymentReconciliationResult result,
        string error) =>
        new()
        {
            PosSquareGross = result.PosSquareGross,
            OutsideSquareGross = result.OutsideSquareGross,
            CombinedSquareGross = result.CombinedSquareGross,
            PosTransactionCount = result.PosTransactionCount,
            OutsideTransactionCount = result.OutsideTransactionCount,
            ActualSquareFees = result.ActualSquareFees,
            ExpectedSquareDeposit = result.ExpectedSquareDeposit,
            LoadedFromSquare = false,
            LoadError = error,
            MatchedPayments = result.MatchedPayments,
            UnmatchedSquarePayments = result.UnmatchedSquarePayments,
            MissingLocalPayments = result.MissingLocalPayments,
            OutsideTerminalProductSales = result.OutsideTerminalProductSales,
            OutsideTerminalCategorySales = result.OutsideTerminalCategorySales,
            OutsideOrdersLoadedCount = result.OutsideOrdersLoadedCount,
            OutsideOrdersMissingCount = result.OutsideOrdersMissingCount,
            Warnings = result.Warnings,
        };
}
