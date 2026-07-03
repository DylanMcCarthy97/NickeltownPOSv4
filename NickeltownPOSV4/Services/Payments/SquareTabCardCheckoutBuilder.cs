using System;
using System.Collections.Generic;
using System.Globalization;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Payments;

public sealed class SquareTabCardCheckoutBuild
{
    public decimal BaseAmount { get; init; }

    public decimal CardFee { get; init; }

    public decimal ChargeTotal { get; init; }

    public List<SquareTerminalLineItem> LineItems { get; init; } = new();

    public SquarePaymentRequest TerminalRequest { get; init; } = new();
}

/// <summary>Shared Square Terminal line items + fee math for bar-tab card flows.</summary>
public static class SquareTabCardCheckoutBuilder
{
    public static SquareTabCardCheckoutBuild Build(
        decimal baseAmount,
        decimal feePercent,
        string primaryLineName,
        string primaryLineCategory,
        string? catalogVariationId,
        string paymentNote,
        string referenceIdPrefix,
        string tabNameForReference)
    {
        var (_, chargeTotal, cardFee) = SquareCardFeeCalculator.CalculateCardTotal(baseAmount, feePercent);
        var lineItems = new List<SquareTerminalLineItem>
        {
            new()
            {
                Name = primaryLineName,
                Quantity = 1,
                UnitPrice = baseAmount,
                Category = primaryLineCategory,
                CatalogObjectId = catalogVariationId?.Trim() ?? string.Empty,
            },
        };

        if (cardFee > 0m)
        {
            lineItems.Add(new SquareTerminalLineItem
            {
                Name = "Card Processing Fee",
                Quantity = 1,
                UnitPrice = cardFee,
            });
        }

        var tabName = string.IsNullOrWhiteSpace(tabNameForReference) ? "Tab" : tabNameForReference.Trim();
        var paymentRequest = new SquarePaymentRequest
        {
            TotalAmount = chargeTotal,
            LineItems = lineItems,
            Note = paymentNote,
            ReferenceId = $"{referenceIdPrefix}-{tabName}-{DateTime.Now:yyyyMMddHHmmss}",
        };

        return new SquareTabCardCheckoutBuild
        {
            BaseAmount = baseAmount,
            CardFee = cardFee,
            ChargeTotal = chargeTotal,
            LineItems = lineItems,
            TerminalRequest = paymentRequest,
        };
    }

    public static SquareCardPaymentRequest ToOrchestratorRequest(
        SquareTabCardCheckoutBuild build,
        string tabLegacyId,
        string? idempotencyKey = null) =>
        new()
        {
            PaymentType = SquarePaymentAttemptType.TabTopUp,
            TabLegacyId = tabLegacyId,
            IdempotencyKey = idempotencyKey,
            BaseAmount = build.BaseAmount,
            SurchargeAmount = build.CardFee,
            ChargedAmount = build.ChargeTotal,
            TerminalRequest = build.TerminalRequest,
        };

    public static string FormatWaitingMessage(string actionPhrase, decimal chargeTotal, decimal cardFee)
    {
        if (cardFee > 0m)
        {
            var lead = string.IsNullOrWhiteSpace(actionPhrase) ? "Charge" : actionPhrase.TrimEnd(' ', '.');
            return $"{lead} {chargeTotal.ToString("C", CultureInfo.CurrentCulture)} on the Square Terminal (includes {cardFee.ToString("C", CultureInfo.CurrentCulture)} fee).";
        }

        return string.IsNullOrWhiteSpace(actionPhrase)
            ? "Complete the payment on the Square Terminal reader."
            : $"{actionPhrase.TrimEnd(' ', '.')}. Complete the payment on the Square Terminal reader.";
    }
}
