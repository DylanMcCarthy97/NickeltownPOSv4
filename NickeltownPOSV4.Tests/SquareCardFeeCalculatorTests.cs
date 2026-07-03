using NickeltownPOSV4.Services;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class SquareCardFeeCalculatorTests
{
    [Fact]
    public void CalculateCardTotal_RoundsToNearestFiveCents()
    {
        var (_, total, fee) = SquareCardFeeCalculator.CalculateCardTotal(10.00m, 1.7m);
        Assert.Equal(10.15m, total);
        Assert.Equal(0.15m, fee);
    }

    [Fact]
    public void CalculateCardTotal_ZeroBase_ReturnsZeros()
    {
        var (_, total, fee) = SquareCardFeeCalculator.CalculateCardTotal(0m, 1.7m);
        Assert.Equal(0m, total);
        Assert.Equal(0m, fee);
    }
}