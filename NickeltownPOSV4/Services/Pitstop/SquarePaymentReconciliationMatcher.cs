using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Services.Pitstop;

public static class SquarePaymentReconciliationMatcher
{
    public const string PosDeviceHint = "Square Terminal 0070";
    public const string OutsideDeviceHint = "Flounderers02";

    private const decimal AmountMismatchTolerance = 0.05m;

    public sealed class SquarePaymentSnapshot
    {
        public string PaymentId { get; init; } = string.Empty;
        public DateTimeOffset PaidAt { get; init; }
        public decimal GrossAmount { get; init; }
        public string? ReceiptNumber { get; init; }
        public string? DeviceName { get; init; }
        public string? CardLast4 { get; init; }
        public decimal ProcessingFees { get; init; }
    }

    public static SquarePaymentReconciliationResult Match(
        IReadOnlyList<SquarePaymentSnapshot> squarePayments,
        IReadOnlyList<PitstopCardSaleRefRow> localCardSales,
        decimal localPosCardTotal,
        decimal squareFeePercentFallback)
    {
        var localByPaymentId = localCardSales
            .Where(s => !string.IsNullOrWhiteSpace(s.SquareExternalRef))
            .GroupBy(s => s.SquareExternalRef.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var matchedSquareIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedRows = new List<SquareReconciliationPaymentRow>();
        var unmatchedRows = new List<SquareReconciliationPaymentRow>();
        var warnings = new List<string>();
        decimal posGross = 0m;
        decimal outsideGross = 0m;
        decimal feeTotal = 0m;
        var hasFeeData = false;

        foreach (var payment in squarePayments)
        {
            if (string.IsNullOrWhiteSpace(payment.PaymentId))
            {
                continue;
            }

            var paymentId = payment.PaymentId.Trim();
            if (payment.ProcessingFees > 0m)
            {
                feeTotal += payment.ProcessingFees;
                hasFeeData = true;
            }

            if (localByPaymentId.TryGetValue(paymentId, out var localSale))
            {
                matchedSquareIds.Add(paymentId);
                matchedRows.Add(new SquareReconciliationPaymentRow
                {
                    PaymentId = paymentId,
                    PaidAt = payment.PaidAt,
                    GrossAmount = payment.GrossAmount,
                    ReceiptNumber = payment.ReceiptNumber,
                    DeviceName = payment.DeviceName,
                    CardLast4 = payment.CardLast4,
                    TerminalClass = SquarePaymentTerminalClass.PosTerminal,
                    LocalSaleId = localSale.SaleId,
                    LocalSaleRef = localSale.SaleRef,
                    LocalSaleAmount = localSale.Total,
                });
                posGross += payment.GrossAmount;

                if (Math.Abs(payment.GrossAmount - localSale.Total) > AmountMismatchTolerance)
                {
                    warnings.Add($"Square payment {paymentId} amount {payment.GrossAmount:C2} differs from local sale {localSale.SaleRef} ({localSale.Total:C2}).");
                }

                AddDeviceMismatchWarning(warnings, payment.DeviceName, paymentId, expectPos: true);
            }
            else
            {
                unmatchedRows.Add(new SquareReconciliationPaymentRow
                {
                    PaymentId = paymentId,
                    PaidAt = payment.PaidAt,
                    GrossAmount = payment.GrossAmount,
                    ReceiptNumber = payment.ReceiptNumber,
                    DeviceName = payment.DeviceName,
                    CardLast4 = payment.CardLast4,
                    TerminalClass = SquarePaymentTerminalClass.OutsideTerminal,
                });
                outsideGross += payment.GrossAmount;
                AddDeviceMismatchWarning(warnings, payment.DeviceName, paymentId, expectPos: false);
            }
        }

        var missingLocal = localCardSales
            .Where(s => !string.IsNullOrWhiteSpace(s.SquareExternalRef) && !matchedSquareIds.Contains(s.SquareExternalRef.Trim()))
            .Select(s => new SquareMissingLocalPaymentRow
            {
                SaleId = s.SaleId,
                SaleRef = s.SaleRef,
                Amount = s.Total,
                PaymentId = s.SquareExternalRef.Trim(),
            })
            .ToList();

        foreach (var missing in missingLocal)
        {
            warnings.Add($"Missing Square payment for local sale {missing.SaleRef} ({missing.Amount:C2}), PaymentId {missing.PaymentId}.");
        }

        posGross = Round(posGross);
        outsideGross = Round(outsideGross);
        var combined = Round(posGross + outsideGross);
        decimal? actualFees = hasFeeData ? Round(feeTotal) : null;
        var feesForDeposit = actualFees ?? Round(combined * (squareFeePercentFallback / 100m));
        var expectedDeposit = Round(combined - feesForDeposit);
        var posDiff = Round(posGross - localPosCardTotal);

        if (Math.Abs(posDiff) > AmountMismatchTolerance)
        {
            warnings.Add($"POS Square total {posGross:C2} differs from Pitstop terminal card {localPosCardTotal:C2} (diff {posDiff:C2}).");
        }

        if (unmatchedRows.Count > 0)
        {
            warnings.Add($"{unmatchedRows.Count} outside-terminal Square payment(s) were not created through ClubPOS.");
        }

        return new SquarePaymentReconciliationResult
        {
            PosSquareGross = posGross,
            OutsideSquareGross = outsideGross,
            CombinedSquareGross = combined,
            PosTransactionCount = matchedRows.Count,
            OutsideTransactionCount = unmatchedRows.Count,
            ActualSquareFees = actualFees,
            ExpectedSquareDeposit = expectedDeposit,
            LoadedFromSquare = true,
            MatchedPayments = matchedRows,
            UnmatchedSquarePayments = unmatchedRows,
            MissingLocalPayments = missingLocal,
            Warnings = warnings,
        };
    }

    private static void AddDeviceMismatchWarning(List<string> warnings, string? deviceName, string paymentId, bool expectPos)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return;
        }

        var isPosDevice = deviceName.Contains(PosDeviceHint, StringComparison.OrdinalIgnoreCase);
        var isOutsideDevice = deviceName.Contains(OutsideDeviceHint, StringComparison.OrdinalIgnoreCase);

        if (expectPos && !isPosDevice && isOutsideDevice)
        {
            warnings.Add($"Payment {paymentId} matched locally but device is {deviceName}.");
        }
        else if (!expectPos && isPosDevice)
        {
            warnings.Add($"Payment {paymentId} is unmatched but device is {deviceName}.");
        }
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
