using System;
using System.Net.Http;
using NickeltownPOSV4.Services;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PosUserMessageTests
{
    [Fact]
    public void CouldNot_TellsStaffWhatFailed()
    {
        Assert.Equal("We couldn't load the activity log. Try again.", PosUserMessage.CouldNotLoad("the activity log"));
        Assert.Equal("We couldn't save the user. Try again.", PosUserMessage.CouldNotSave("the user"));
        Assert.Equal("We couldn't open the drawer. Try again.", PosUserMessage.CouldNot("open the drawer"));
    }

    [Fact]
    public void FromException_MapsKnownTypes()
    {
        Assert.Equal(PosUserMessage.Cancelled, PosUserMessage.FromException(new OperationCanceledException()));
        Assert.Equal(PosUserMessage.NoPermission, PosUserMessage.FromException(new UnauthorizedAccessException("denied")));
        Assert.Equal(PosUserMessage.Network, PosUserMessage.FromException(new HttpRequestException("boom")));
        Assert.Equal(PosUserMessage.TimedOut, PosUserMessage.FromException(new TimeoutException()));
    }

    [Fact]
    public void FromException_KeepsPlainOperatorMessages()
    {
        var ex = new InvalidOperationException("Enter a tab name.");
        Assert.Equal("Enter a tab name.", PosUserMessage.FromException(ex));
    }

    [Theory]
    [InlineData("SQLite Error 5: database is locked")]
    [InlineData("UNIQUE constraint failed: Bartenders.Pin")]
    [InlineData("Value cannot be null. (Parameter 'name')")]
    [InlineData("Object reference not set to an instance of an object.")]
    [InlineData("JSON could not be parsed: Unexpected character")]
    [InlineData("Square API response could not confirm checkout creation: ACCESS_TOKEN_EXPIRED")]
    public void FromException_HidesTechnicalText(string technical)
    {
        var ex = new InvalidOperationException(technical);
        Assert.Equal("We couldn't save the tab. Try again.", PosUserMessage.FromException(ex, PosUserMessage.CouldNotSave("the tab")));
    }

    [Fact]
    public void FromException_UsesFallbackForUnknownExceptions()
    {
        var ex = new Exception("Microsoft.Data.Sqlite.SqliteException: disk I/O error");
        Assert.Equal(PosUserMessage.Generic, PosUserMessage.FromException(ex));
        Assert.Equal(PosUserMessage.CouldNotLoad("users"), PosUserMessage.FromException(ex, PosUserMessage.CouldNotLoad("users")));
    }

    [Fact]
    public void OrFallback_KeepsPlainTextOnly()
    {
        Assert.Equal("Enter a tab name.", PosUserMessage.OrFallback("Enter a tab name.", PosUserMessage.Generic));
        Assert.Equal(PosUserMessage.Generic, PosUserMessage.OrFallback("SQLite Error 5: database is locked", PosUserMessage.Generic));
    }

    [Fact]
    public void CardCopy_TellsStaffNotToChargeAgainWithoutAdmin()
    {
        Assert.Contains("Do not run the card again", PosUserMessage.CardNeedsChecking, StringComparison.Ordinal);
        Assert.Contains("Do not charge them again", PosUserMessage.CardApprovedUnsaved, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin", PosUserMessage.CardNeedsChecking, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Admin", PosUserMessage.CardApprovedUnsaved, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fix payments", PosUserMessage.CardNeedsChecking, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fix payments", PosUserMessage.CardApprovedUnsaved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LooksTechnical_DetectsInfrastructureJargon()
    {
        Assert.True(PosUserMessage.LooksTechnical("SQLite database is locked"));
        Assert.True(PosUserMessage.LooksTechnical("Missing idempotency key"));
        Assert.True(PosUserMessage.LooksTechnical("Square API response could not confirm checkout creation: ACCESS_TOKEN_EXPIRED"));
        Assert.False(PosUserMessage.LooksTechnical("That PIN is already in use. Pick a different 4-digit PIN."));
        Assert.False(PosUserMessage.LooksTechnical("Not enough stock for “Coke” (have 1, need 2)."));
    }
}
