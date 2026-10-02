using NickeltownPOSV4.Services.Pitstop;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopPayButtonRulesTests
{
    [Fact]
    public void CanStart_EmptyCart_IsDisabled()
    {
        Assert.False(PitstopPayButtonRules.CanStart(0, isPaymentLocked: false, isSendingSquare: false));
    }

    [Fact]
    public void CanStart_CartHasItems_IsEnabled()
    {
        Assert.True(PitstopPayButtonRules.CanStart(1, isPaymentLocked: false, isSendingSquare: false));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CanStart_LockedOrSendingSquare_IsDisabled(bool locked, bool sendingSquare)
    {
        Assert.False(PitstopPayButtonRules.CanStart(2, locked, sendingSquare));
    }
}
