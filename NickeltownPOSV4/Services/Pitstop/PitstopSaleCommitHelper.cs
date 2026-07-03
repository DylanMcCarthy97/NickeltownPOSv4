using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Pitstop;

internal static class PitstopSaleCommitHelper
{
    public static List<PitstopSaleLineCommit> ToSaleLines(IEnumerable<PitstopCartLineViewModel> cart) =>
        cart
            .Select(l => new PitstopSaleLineCommit
            {
                ItemId = l.ItemId,
                DisplayName = l.DisplayName,
                Sku = l.Sku,
                UnitPrice = l.UnitPrice,
                Quantity = l.Quantity,
                CategoryName = l.CategoryName,
                SubCategory = l.SubCategory,
            })
            .ToList();

    public static PitstopSalePaymentCommit BuildPaymentCommit(
        PitstopPaymentSelection payment,
        decimal baseTotal,
        decimal charged,
        decimal cardSurchargePercent,
        decimal pendingCardFee,
        string? squareExternalRef,
        string? squareCheckoutId,
        string? idempotencyKey,
        long? paymentAttemptId,
        decimal? cashReceived,
        decimal? cashChange,
        long? staffId,
        string? staffDisplayName)
    {
        var method = payment == PitstopPaymentSelection.Cash ? "Cash" : "Card";
        decimal? surchargePct = null;
        decimal? surchargeAmt = null;

        if (payment == PitstopPaymentSelection.Card)
        {
            surchargePct = cardSurchargePercent;
            surchargeAmt = pendingCardFee > 0m ? pendingCardFee : null;
        }

        return new PitstopSalePaymentCommit
        {
            PaymentMethod = method,
            BartenderId = staffId,
            StaffDisplayName = staffDisplayName,
            SquareExternalRef = squareExternalRef,
            SquareCheckoutId = squareCheckoutId,
            IdempotencyKey = idempotencyKey,
            PaymentAttemptId = paymentAttemptId,
            CashReceived = cashReceived,
            CashChange = cashChange,
            BaseProductTotal = baseTotal,
            CardSurchargePercent = surchargePct,
            CardSurchargeAmount = surchargeAmt,
            ChargedTotal = charged,
        };
    }

    public static string FormatRecordedSuccessMessage(
        PitstopPaymentSelection payment,
        decimal charged,
        decimal? cashReceived,
        decimal? cashChange)
    {
        var inv = CultureInfo.InvariantCulture;
        if (payment == PitstopPaymentSelection.Cash)
        {
            var changePart = cashChange.HasValue
                ? $" Change ${cashChange.Value.ToString("0.00", inv)}."
                : string.Empty;
            var receivedPart = cashReceived.HasValue
                ? $" (received ${cashReceived.Value.ToString("0.00", inv)})"
                : string.Empty;
            return $"Recorded: ${charged.ToString("0.00", inv)} cash for Pitstop sale.{receivedPart}{changePart}";
        }

        return $"Recorded: ${charged.ToString("0.00", inv)} card for Pitstop sale.";
    }

    public const string PaymentNotRecordedMessage = "Not recorded. No tab/sale was updated.";

    public static string FormatSavedStatusMessage(
        PitstopPaymentSelection payment,
        decimal charged,
        decimal baseTotal,
        decimal? cashTendered,
        decimal? cashChange)
    {
        var inv = CultureInfo.InvariantCulture;
        if (payment == PitstopPaymentSelection.Cash && cashChange.HasValue)
        {
            return
                $"Sale saved. Change due: ${cashChange.Value.ToString("0.00", inv)}"
                + (cashTendered.HasValue ? $" (received ${cashTendered.Value.ToString("0.00", inv)})" : string.Empty);
        }

        if (payment == PitstopPaymentSelection.Card)
        {
            return
                $"Card sale saved. Charged ${charged.ToString("0.00", inv)} (product total ${baseTotal.ToString("0.00", inv)}).";
        }

        return "Sale saved.";
    }

    public static decimal ResolveCardChargeTotal(
        decimal baseTotal,
        decimal pendingCardChargeTotal) =>
        pendingCardChargeTotal > 0m ? pendingCardChargeTotal : baseTotal;
}
