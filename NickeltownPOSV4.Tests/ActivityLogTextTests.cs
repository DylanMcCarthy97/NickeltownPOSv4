using NickeltownPOSV4.Models.Audit;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class ActivityLogTextTests
{
    [Fact]
    public void FormatLine_MatchesClubroomExample()
    {
        var localNow = DateTimeOffset.Now;
        var occurred = new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 19, 42, 0, localNow.Offset);
        var entry = new AuditLogEntry
        {
            OccurredAt = occurred,
            StaffName = "Dylan",
            ActionType = AuditActions.TabPurchaseUndone,
            Reason = "Undid 1 × Great Northern | Smith tab",
            Success = true,
        };

        var line = ActivityLogText.FormatLine(entry, localNow);

        Assert.Equal("7:42pm | Dylan | Undid 1 × Great Northern | Smith tab", line);
    }

    [Fact]
    public void UndidDrinks_SingleLineIncludesTab()
    {
        var text = ActivityLogText.UndidDrinks([("Great Northern", 1)], "Smith");
        Assert.Equal("Undid 1 × Great Northern | Smith tab", text);
    }

    [Fact]
    public void FundsAdded_ManualNegativeIsBalanceAdjust()
    {
        var text = ActivityLogText.FundsAdded("manual", -5m, "Smith");
        Assert.Equal("Adjusted balance -$5.00 | Smith tab", text);
    }

    [Fact]
    public void ArchivedAndReopened_UseTabName()
    {
        Assert.Equal("Archived Smith tab", ActivityLogText.ArchivedTab("Smith"));
        Assert.Equal("Reopened Smith tab", ActivityLogText.ReopenedTab("Smith tab"));
    }

    [Fact]
    public void StockAdjusted_ShowsBeforeAndAfter()
    {
        Assert.Equal("Adjusted Great Northern stock 12 → 10", ActivityLogText.StockAdjusted("Great Northern", 12, 10));
    }

    [Fact]
    public void ComplimentaryItemRecorded_HasNoMemberName()
    {
        Assert.Equal("Recorded 1 × Water as a free member item", ActivityLogText.ComplimentaryItemRecorded("Water", 1));
        Assert.Equal("Undid 1 × Pop Top free member item", ActivityLogText.ComplimentaryItemUndone("Pop Top", 1));
        Assert.DoesNotContain("tab", ActivityLogText.ComplimentaryItemRecorded("Water", 1), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatLine_FailedEntriesAreMarked()
    {
        var now = DateTimeOffset.Now;
        var entry = new AuditLogEntry
        {
            OccurredAt = now,
            StaffName = "Dylan",
            ActionType = AuditActions.TabFundsUndone,
            Reason = "Undid $20.00 Square card | Smith tab",
            Success = false,
        };

        Assert.EndsWith("(failed)", ActivityLogText.FormatLine(entry, now));
    }
}
