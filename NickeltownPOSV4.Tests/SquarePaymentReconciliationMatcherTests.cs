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
    public void Match_OnlyFlounderers02CountsAsOutside_UnknownDevicesExcluded()
    {
        var square = new[]
        {
            Snap("pay-unknown", 15.00m, "Some Other Device"),
            Snap("pay-blank", 12.00m, null),
            Snap("pay-merch", 40.00m, SquarePaymentReconciliationMatcher.OutsideDeviceHint),
            Snap("pay-0070-short", 99.00m, "Terminal 0070"),
        };

        var result = SquarePaymentReconciliationMatcher.Match(square, Array.Empty<PitstopCardSaleRefRow>(), 0m, 1.75m);

        Assert.Equal(0m, result.PosSquareGross);
        Assert.Equal(40.00m, result.OutsideSquareGross);
        Assert.Equal(1, result.OutsideTransactionCount);
        Assert.Equal(3, result.ExcludedNonPitstopTransactionCount);
        Assert.Equal(126.00m, result.ExcludedNonPitstopGross);
    }

    [Fact]
    public void Match_PosBucketOnlyIncludesPitstopMatchedSales()
    {
        var square = new[]
        {
            Snap("pay-pitstop", 10.17m, "Square Terminal 0070"),
            Snap("pay-tab", 50.00m, "Square Terminal 0070"),
        };
        var local = new[] { Local(1, "sale-1", 10.17m, "pay-pitstop") };

        var result = SquarePaymentReconciliationMatcher.Match(square, local, 10.17m, 1.75m);

        Assert.Equal(10.17m, result.PosSquareGross);
        Assert.Equal(0m, result.OutsideSquareGross);
        Assert.Equal(1, result.PosTransactionCount);
        Assert.Equal(0, result.OutsideTransactionCount);
        Assert.Equal(1, result.ExcludedNonPitstopTransactionCount);
        Assert.Equal(50.00m, result.ExcludedNonPitstopGross);
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
