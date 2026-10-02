using NickeltownPOSV4.ViewModels;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class TouchNumpadOverlayViewModelTests
{
    [Fact]
    public void FirstDigit_ReplacesExistingInteger()
    {
        var vm = new TouchNumpadOverlayViewModel(
            24m,
            "Pack size",
            false,
            NumpadMode.Integer,
            0,
            false,
            true,
            _ => { },
            null);

        vm.DigitCommand.Execute("8");

        Assert.Equal("8", vm.AmountDisplay);
    }

    [Fact]
    public void FirstBackspace_ClearsExistingInteger()
    {
        var vm = new TouchNumpadOverlayViewModel(
            24m,
            "Pack size",
            false,
            NumpadMode.Integer,
            0,
            false,
            true,
            _ => { },
            null);

        vm.BackspaceCommand.Execute(null);

        Assert.Equal("0", vm.AmountDisplay);
    }

    [Fact]
    public void AfterReplace_FurtherDigitsAppend()
    {
        var vm = new TouchNumpadOverlayViewModel(
            24m,
            "Pack size",
            false,
            NumpadMode.Integer,
            0,
            false,
            true,
            _ => { },
            null);

        vm.DigitCommand.Execute("1");
        vm.DigitCommand.Execute("2");

        Assert.Equal("12", vm.AmountDisplay);
    }

    [Fact]
    public void ResetCurrencyDraft_FirstDigitReplacesPrefill()
    {
        var vm = new TouchNumpadOverlayViewModel(0m, "Received", false, _ => { });
        vm.ResetCurrencyDraft(10.00m);

        vm.DigitCommand.Execute("5");

        Assert.Equal("5.00", vm.AmountDisplay);
    }
}
