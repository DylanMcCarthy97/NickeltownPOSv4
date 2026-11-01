using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopCardFixGuideTests
{
    [Fact]
    public void AskCheck_TellsStaffNotToChargeAgain()
    {
        var copy = PitstopCardFixGuide.Build(PitstopCardFixStep.AskCheck, 12.50m, canRebuildSale: true);
        Assert.Equal("This card payment didn't finish", copy.Title);
        Assert.Contains("Do not run the card again", copy.Body, StringComparison.Ordinal);
        Assert.True(copy.ShowCheck);
        Assert.False(copy.ShowNotCharged);
        Assert.False(copy.ShowSaveSale);
    }

    [Fact]
    public void Paid_WithPayload_OffersSaveOnly()
    {
        var copy = PitstopCardFixGuide.Build(PitstopCardFixStep.Paid, 8m, canRebuildSale: true);
        Assert.Equal("They paid", copy.Title);
        Assert.True(copy.ShowSaveSale);
        Assert.False(copy.ShowNotCharged);
        Assert.Contains("Do not charge them again", copy.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Paid_WithoutPayload_DoesNotOfferSave()
    {
        var copy = PitstopCardFixGuide.Build(PitstopCardFixStep.Paid, 8m, canRebuildSale: false);
        Assert.True(copy.ShowOk);
        Assert.False(copy.ShowSaveSale);
    }

    [Fact]
    public void NotCharged_SendsThemBackToCash()
    {
        var copy = PitstopCardFixGuide.Build(PitstopCardFixStep.NotCharged, 20m, canRebuildSale: false);
        Assert.Contains("Take cash", copy.Body, StringComparison.Ordinal);
        Assert.True(copy.ShowOk);
        Assert.False(copy.ShowSaveSale);
    }

    [Fact]
    public void OpeningStep_SkipsCheckWhenThereIsNothingToAsk()
    {
        Assert.Equal(PitstopCardFixStep.AskCheck, PitstopCardFixGuide.OpeningStep(theyPaid: false, hasCheckoutId: true));
        Assert.Equal(PitstopCardFixStep.Unknown, PitstopCardFixGuide.OpeningStep(theyPaid: false, hasCheckoutId: false));
        Assert.Equal(PitstopCardFixStep.Paid, PitstopCardFixGuide.OpeningStep(theyPaid: true, hasCheckoutId: false));
    }

    [Fact]
    public void AfterSquareCheck_MapsSafeNextStep()
    {
        Assert.Equal(PitstopCardFixStep.Paid, PitstopCardFixGuide.AfterSquareCheck(approved: true, confirmedNotPaid: false, canRebuildSale: true));
        Assert.Equal(PitstopCardFixStep.NotCharged, PitstopCardFixGuide.AfterSquareCheck(approved: false, confirmedNotPaid: true, canRebuildSale: false));
        Assert.Equal(PitstopCardFixStep.Unknown, PitstopCardFixGuide.AfterSquareCheck(approved: false, confirmedNotPaid: false, canRebuildSale: true));
    }

    [Fact]
    public void Banner_UsesAmountOnly()
    {
        Assert.StartsWith("A card still needs a look", PitstopCardFixGuide.BannerText(15m), StringComparison.Ordinal);
        Assert.DoesNotContain("Square", PitstopCardFixGuide.BannerText(15m), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Admin", PitstopCardFixGuide.BannerText(15m), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PitstopCardFixStep.AskCheck, true)]
    [InlineData(PitstopCardFixStep.Paid, true)]
    [InlineData(PitstopCardFixStep.Paid, false)]
    [InlineData(PitstopCardFixStep.NotCharged, false)]
    [InlineData(PitstopCardFixStep.Unknown, true)]
    [InlineData(PitstopCardFixStep.Saved, true)]
    public void Copy_NeverSendsStaffToAdminOrPin(PitstopCardFixStep step, bool canRebuildSale)
    {
        var copy = PitstopCardFixGuide.Build(step, 11.25m, canRebuildSale);
        var text = string.Join(' ', copy.Title, copy.Body, copy.CheckLabel, copy.SaveLabel, copy.NotChargedLabel, copy.OkLabel);
        Assert.DoesNotContain("Admin", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PIN", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Square", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotency", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("checkout", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_LetsThemClearWithoutAdmin()
    {
        var copy = PitstopCardFixGuide.Build(PitstopCardFixStep.Unknown, 7m, canRebuildSale: false);
        Assert.True(copy.ShowCheck);
        Assert.True(copy.ShowNotCharged);
        Assert.False(copy.ShowSaveSale);
        Assert.Contains("They weren't charged", copy.Body, StringComparison.Ordinal);
    }
}
