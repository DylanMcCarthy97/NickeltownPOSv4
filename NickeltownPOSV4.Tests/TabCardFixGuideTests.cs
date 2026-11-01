using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Services.Tabs;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class TabCardFixGuideTests
{
    [Fact]
    public void NotCharged_RetriesSameTabPayment()
    {
        var copy = TabCardFixGuide.Build(PitstopCardFixStep.NotCharged, 20m, canRebuildCredit: false);
        Assert.Contains("Try the card again", copy.Body, StringComparison.Ordinal);
        Assert.True(copy.ShowRetryCard);
        Assert.True(copy.ShowTakeCash);
        Assert.DoesNotContain("Admin", copy.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("new sale", copy.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Paid_WithSave_DoesNotSendToAdmin()
    {
        var copy = TabCardFixGuide.Build(PitstopCardFixStep.Paid, 15m, canRebuildCredit: true);
        Assert.Contains("Do not charge them again", copy.Body, StringComparison.Ordinal);
        Assert.True(copy.ShowSaveSale);
        Assert.Equal("Save the credit", copy.SaveLabel);
    }

    [Fact]
    public void Saved_UsesPlainNoun()
    {
        var copy = TabCardFixGuide.Build(PitstopCardFixStep.Saved, 10m, canRebuildCredit: true, savedNoun: "payment");
        Assert.Contains("payment is on the tab", copy.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Square", copy.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Admin", copy.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PIN", copy.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(PitstopCardFixStep.AskCheck, true)]
    [InlineData(PitstopCardFixStep.Checking, true)]
    [InlineData(PitstopCardFixStep.Saving, true)]
    [InlineData(PitstopCardFixStep.Paid, true)]
    [InlineData(PitstopCardFixStep.Paid, false)]
    [InlineData(PitstopCardFixStep.NotCharged, false)]
    [InlineData(PitstopCardFixStep.Unknown, true)]
    [InlineData(PitstopCardFixStep.Saved, true)]
    public void Copy_NeverSendsStaffToAdminOrPin(PitstopCardFixStep step, bool canRebuild)
    {
        var copy = TabCardFixGuide.Build(step, 11.25m, canRebuild);
        var text = string.Join(' ', copy.Title, copy.Body, copy.CheckLabel, copy.SaveLabel, copy.NotChargedLabel, copy.OkLabel, copy.RetryCardLabel, copy.TakeCashLabel);
        Assert.DoesNotContain("Admin", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PIN", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Square", text, StringComparison.OrdinalIgnoreCase);
    }
}
