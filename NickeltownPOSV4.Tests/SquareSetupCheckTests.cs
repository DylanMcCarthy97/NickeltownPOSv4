using NickeltownPOSV4.ViewModels;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class SquareSetupCheckTests
{
    [Fact]
    public void ShopSet_IsOk()
    {
        var row = SquareSetupCheck.ShopRow("LOC_123", readerIsSet: true);
        Assert.Equal("Shop ID", row.Label);
        Assert.Equal("Set", row.Value);
        Assert.Equal(SystemCheckSeverity.Ok, row.Severity);
        Assert.DoesNotContain("Admin", row.Hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Square shop", row.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShopMissing_WithReader_IsWarningNotError()
    {
        var row = SquareSetupCheck.ShopRow("", readerIsSet: true);
        Assert.Equal("Not set", row.Value);
        Assert.Equal(SystemCheckSeverity.Warning, row.Severity);
        Assert.Contains("Cards still work", row.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin", row.Hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShopMissing_WithoutReader_IsError()
    {
        var row = SquareSetupCheck.ShopRow(null, readerIsSet: false);
        Assert.Equal(SystemCheckSeverity.Error, row.Severity);
        Assert.Contains("Ask a manager", row.Hint, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin", row.Hint, StringComparison.OrdinalIgnoreCase);
    }
}
