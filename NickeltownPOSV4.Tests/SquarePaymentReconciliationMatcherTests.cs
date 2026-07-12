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
            Snap("pay-pos-1", 10.17m),
            Snap("pay-outside-1", 25.00m),
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
    }

    [Fact]
    public void Match_FlagsMissingSquarePaymentForLocalSale()
    {
        var square = new[] { Snap("pay-pos-1", 10.17m) };
        var local = new[]
        {
            Local(1, "sale-1", 10.17m, "pay-pos-1"),
            Local(2, "sale-2", 8.50m, "pay-missing"),
        };

        var result = SquarePaymentReconciliationMatcher.Match(square, local, 10.17m, 1.75m);

        Assert.Single(result.MissingLocalPayments);
    }

    private static SquarePaymentReconciliationMatcher.SquarePaymentSnapshot Snap(string id, decimal amount) =>
        new() { PaymentId = id, GrossAmount = amount, PaidAt = DateTimeOffset.UtcNow };

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
