using System.Linq;
using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopCashPaymentHelperTests
{
    [Fact]
    public void BuildQuickTenders_IncludesExactThenNoteRoundUps()
    {
        var tenders = PitstopCashPaymentHelper.BuildQuickTenders(17.50m);

        Assert.Equal("Exact", tenders[0].Label);
        Assert.Equal(17.50m, tenders[0].Amount);
        Assert.True(tenders[0].IsExact);

        Assert.Contains(tenders, t => t.Amount == 20m);
        Assert.Contains(tenders, t => t.Amount == 50m);
        Assert.Contains(tenders, t => t.Amount == 100m);
        Assert.True(tenders.Count <= 5);
    }

    [Fact]
    public void BuildQuickTenders_WhenTotalIsExactNote_OffersNextHigherNotes()
    {
        var tenders = PitstopCashPaymentHelper.BuildQuickTenders(20m);

        Assert.Equal(20m, tenders[0].Amount);
        Assert.DoesNotContain(tenders.Skip(1), t => t.Amount == 20m);
        Assert.Contains(tenders, t => t.Amount == 25m);
        Assert.Contains(tenders, t => t.Amount == 50m);
    }

    [Theory]
    [InlineData(20, 17.50, "$2.50")]
    [InlineData(17.50, 17.50, "No change")]
    [InlineData(10, 17.50, "Short $7.50")]
    [InlineData(0, 17.50, "\u2014")]
    public void FormatChangePreview_MatchesTillExpectations(decimal received, decimal total, string expected)
    {
        Assert.Equal(expected, PitstopCashPaymentHelper.FormatChangePreview(received, total));
    }

    [Fact]
    public void FormatTenderLabel_DropsCentsForWholeNotes()
    {
        Assert.Equal("$20", PitstopCashPaymentHelper.FormatTenderLabel(20m));
        Assert.Equal("$17.50", PitstopCashPaymentHelper.FormatTenderLabel(17.50m));
    }
}