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
        var excludedRows = new List<SquareReconciliationPaymentRow>();
        var warnings = new List<string>();
        decimal posGross = 0m;
        decimal outsideGross = 0m;
        decimal excludedGross = 0m;
        decimal feeTotal = 0m;
        var hasFeeData = false;

        foreach (var payment in squarePayments)
        {
            if (string.IsNullOrWhiteSpace(payment.PaymentId))
            {
                continue;
            }

            var paymentId = payment.PaymentId.Trim();

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
                AddFees(payment, ref feeTotal, ref hasFeeData);

                if (Math.Abs(payment.GrossAmount - localSale.Total) > AmountMismatchTolerance)
                {
                    warnings.Add($"Square payment {paymentId} amount {payment.GrossAmount:C2} differs from local sale {localSale.SaleRef} ({localSale.Total:C2}).");
                }

                AddDeviceMismatchWarning(warnings, payment.DeviceName, paymentId, expectPos: true);
                continue;
            }

            // Outside = Flounderers02 only.
            // Unmatched 0070 (bar tabs / other POS card) and unknown devices are excluded —
            // never dumped into Pitstop outside merch.
            if (IsOutsideDevice(payment.DeviceName))
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
                AddFees(payment, ref feeTotal, ref hasFeeData);
                continue;
            }

            excludedRows.Add(new SquareReconciliationPaymentRow
            {
                PaymentId = paymentId,
                PaidAt = payment.PaidAt,
                GrossAmount = payment.GrossAmount,
                ReceiptNumber = payment.ReceiptNumber,
                DeviceName = payment.DeviceName,
                CardLast4 = payment.CardLast4,
                TerminalClass = IsPosDevice(payment.DeviceName)
                    ? SquarePaymentTerminalClass.PosTerminal
                    : SquarePaymentTerminalClass.Unknown,
            });
            excludedGross += payment.GrossAmount;
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
        excludedGross = Round(excludedGross);
        var combined = Round(posGross + outsideGross);
        decimal? actualFees = hasFeeData ? Round(feeTotal) : null;
        var feesForDeposit = actualFees ?? Round(combined * (squareFeePercentFallback / 100m));
        var expectedDeposit = Round(combined - feesForDeposit);
        var posDiff = Round(posGross - localPosCardTotal);

        if (Math.Abs(posDiff) > AmountMismatchTolerance)
        {
            warnings.Add($"POS Square total {posGross:C2} differs from Pitstop terminal card {localPosCardTotal:C2} (diff {posDiff:C2}).");
        }

        if (excludedRows.Count > 0)
        {
            var posExcluded = excludedRows.Count(r => IsPosDevice(r.DeviceName));
            var otherExcluded = excludedRows.Count - posExcluded;
            if (posExcluded > 0)
            {
                warnings.Add(
                    $"Excluded {posExcluded} Square payment(s) on {PosDeviceHint} that are not Pitstop sales "
                    + $"(e.g. bar tab top-ups), total {Round(excludedRows.Where(r => IsPosDevice(r.DeviceName)).Sum(r => r.GrossAmount)):C2}.");
            }

            if (otherExcluded > 0)
            {
                warnings.Add(
                    $"Excluded {otherExcluded} Square payment(s) not on {OutsideDeviceHint} and not matched to Pitstop "
                    + "(not counted as outside merch).");
            }
        }

        if (unmatchedRows.Count > 0)
        {
            warnings.Add($"{unmatchedRows.Count} {OutsideDeviceHint} Square payment(s) were not created through ClubPOS Pitstop.");
        }

        return new SquarePaymentReconciliationResult
        {
            PosSquareGross = posGross,
            OutsideSquareGross = outsideGross,
            CombinedSquareGross = combined,
            PosTransactionCount = matchedRows.Count,
            OutsideTransactionCount = unmatchedRows.Count,
            ExcludedNonPitstopGross = excludedGross,
            ExcludedNonPitstopTransactionCount = excludedRows.Count,
            ActualSquareFees = actualFees,
            ExpectedSquareDeposit = expectedDeposit,
            LoadedFromSquare = true,
            MatchedPayments = matchedRows,
            UnmatchedSquarePayments = unmatchedRows,
            ExcludedNonPitstopPayments = excludedRows,
            MissingLocalPayments = missingLocal,
            Warnings = warnings,
        };
    }

    internal static bool IsPosDevice(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return false;
        }

        // Prefer full name; also accept any device label containing 0070 (bar POS terminal).
        return deviceName.Contains(PosDeviceHint, StringComparison.OrdinalIgnoreCase)
            || deviceName.Contains("0070", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOutsideDevice(string? deviceName) =>
        !string.IsNullOrWhiteSpace(deviceName)
        && deviceName.Contains(OutsideDeviceHint, StringComparison.OrdinalIgnoreCase);

    private static void AddFees(SquarePaymentSnapshot payment, ref decimal feeTotal, ref bool hasFeeData)
    {
        if (payment.ProcessingFees > 0m)
        {
            feeTotal += payment.ProcessingFees;
            hasFeeData = true;
        }
    }

    private static void AddDeviceMismatchWarning(List<string> warnings, string? deviceName, string paymentId, bool expectPos)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return;
        }

        var isPosDevice = IsPosDevice(deviceName);
        var isOutsideDevice = IsOutsideDevice(deviceName);

        if (expectPos && !isPosDevice && isOutsideDevice)
        {
            warnings.Add($"Payment {paymentId} matched locally but device is {deviceName}.");
        }
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
