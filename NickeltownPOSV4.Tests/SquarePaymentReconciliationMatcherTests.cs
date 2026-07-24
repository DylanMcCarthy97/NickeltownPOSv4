using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class SquarePaymentReconciliationMatcherTests
{
    [Fact]
    public void Match_ClassifiesByLocalPaymentReference()
    {
        var square = new[]
        {
            Snap("pay-pos-1", 10.17m, SquarePaymentReconciliationMatcher.PosDeviceHint),
            Snap("pay-outside-1", 25.00m, SquarePaymentReconciliationMatcher.OutsideDeviceHint),
        };

        var local = new[]
        {
            Local(1, "sale-1", 10.17m, "pay-pos-1"),
        };

        var result = SquarePaymentReconciliationMatcher.Match(square, local, 10.17m, 1.75m);

        Assert.Equal(10.17m, result.PosSquareGross);
        Assert.Equal(25.00m, result.OutsideSquareGross);
        Assert.Equal(35.17m, result.CombinedSquareGross);
        Assert.Equal(1, result.PosTransactionCount);
        Assert.Equal(1, result.OutsideTransactionCount);
        Assert.Equal(0, result.ExcludedNonPitstopTransactionCount);
    }

    [Fact]
    public void Match_ExcludesUnmatchedPosTerminalPaymentsFromOutside()
    {
        var square = new[]
        {
            Snap("pay-tab-1", 101.70m, SquarePaymentReconciliationMatcher.PosDeviceHint),
            Snap("pay-tab-2", 152.55m, SquarePaymentReconciliationMatcher.PosDeviceHint),
            Snap("pay-merch-1", 40.00m, SquarePaymentReconciliationMatcher.OutsideDeviceHint),
        };

        var result = SquarePaymentReconciliationMatcher.Match(square, Array.Empty<PitstopCardSaleRefRow>(), 0m, 1.75m);

        Assert.Equal(0m, result.PosSquareGross);
        Assert.Equal(40.00m, result.OutsideSquareGross);
        Assert.Equal(40.00m, result.CombinedSquareGross);
        Assert.Equal(0, result.PosTransactionCount);
        Assert.Equal(1, result.OutsideTransactionCount);
        Assert.Equal(2, result.ExcludedNonPitstopTransactionCount);
        Assert.Equal(254.25m, result.ExcludedNonPitstopGross);
        Assert.Contains(result.Warnings, w => w.Contains("Excluded 2 Square payment", StringComparison.Ordinal));
    }

    [Fact]
    public void Match_FlagsMissingSquarePaymentForLocalSale()
    {
        var square = new[] { Snap("pay-pos-1", 10.17m, SquarePaymentReconciliationMatcher.PosDeviceHint) };
        var local = new[]
        {
            Local(1, "sale-1", 10.17m, "pay-pos-1"),
            Local(2, "sale-2", 8.50m, "pay-missing"),
        };

        var result = SquarePaymentReconciliationMatcher.Match(square, local, 10.17m, 1.75m);

        Assert.Single(result.MissingLocalPayments);
    }

    private static SquarePaymentReconciliationMatcher.SquarePaymentSnapshot Snap(
        string id,
        decimal amount,
        string? deviceName = null) =>
        new()
        {
            PaymentId = id,
            GrossAmount = amount,
            PaidAt = DateTimeOffset.UtcNow,
            DeviceName = deviceName,
        };

    private static PitstopCardSaleRefRow Local(long id, string saleRef, decimal total, string paymentId) =>
        new()
        {
            SaleId = id,
            SaleRef = saleRef,
            Total = total,
            SquareExternalRef = paymentId,
            SoldAt = DateTimeOffset.UtcNow,
        };
}
