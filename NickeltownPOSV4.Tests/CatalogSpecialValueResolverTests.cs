using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.TabFavorites;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class CatalogSpecialValueResolverTests
{
    [Theory]
    [InlineData("10", 30, 27)]
    [InlineData("10%", 30, 27)]
    public void PercentOff_computes_sale_price(string input, decimal regular, decimal expected)
    {
        Assert.True(
            CatalogSpecialValueResolver.TryResolveSaleUnitPrice(
                CatalogSpecialValueResolver.PercentOff,
                input,
                regular,
                out var sale,
                out var err),
            err);
        Assert.Equal(expected, sale);
    }

    [Fact]
    public void FixedPrice_uses_entered_amount()
    {
        Assert.True(
            CatalogSpecialValueResolver.TryResolveSaleUnitPrice(
                CatalogSpecialValueResolver.FixedPrice,
                "10.00",
                30m,
                out var sale,
                out _));
        Assert.Equal(10m, sale);
    }
}

public sealed class BarCatalogPriceResolverTests
{
    [Theory]
    [InlineData(5, 10, "Shared", 5)]
    [InlineData(0, 10, "Shared", 10)]
    [InlineData(0, 10, "Bar", 0)]
    [InlineData(7, 10, "Pitstop", 7)]
    public void ResolveEffectivePitstopPrice_prefers_pitstop_then_shared_bar(
        double pitstop,
        double bar,
        string bucket,
        double expected)
    {
        Assert.Equal(expected, BarCatalogPriceResolver.ResolveEffectivePitstopPrice(pitstop, bar, bucket));
    }
}

public sealed class TabFavoritesCalculatorTests
{
    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(10, true)]
    public void QualifiesByMonthlyOrders_requires_more_than_five(int monthlyCount, bool expected)
    {
        Assert.Equal(expected, TabFavoritesCalculator.QualifiesByMonthlyOrders(monthlyCount));
    }

    [Fact]
    public void GetEffectiveFavoriteItemIds_excludes_low_monthly_history_but_keeps_manual_pins()
    {
        var result = TabFavoritesCalculator.GetEffectiveFavoriteItemIds(
            historyCounts: new List<(long, int)> { (1L, 3), (2L, 6), (3L, 5) },
            sessionCounts: new Dictionary<long, int> { [4] = 10 },
            manualFavoriteItemIds: new List<long> { 99 });

        Assert.Equal(new[] { 99L, 2L }, result);
    }

    [Fact]
    public void GetEffectiveFavoriteItemIds_session_boost_only_when_monthly_qualifies()
    {
        var result = TabFavoritesCalculator.GetEffectiveFavoriteItemIds(
            historyCounts: new List<(long, int)> { (1L, 6) },
            sessionCounts: new Dictionary<long, int> { [1] = 3, [2] = 5 },
            manualFavoriteItemIds: Array.Empty<long>());

        Assert.Single(result);
        Assert.Equal(1L, result[0]);
    }
}
