using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Tabs;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class TabUndoPreviewTests
{
    private static readonly DateTimeOffset EightOhTwo =
        new(2026, 8, 18, 20, 2, 0, TimeSpan.FromHours(10));

    [Fact]
    public void ForDrinks_MatchesBartenderConfirmationCard()
    {
        var preview = TabUndoPreview.ForDrinks(
            [new TabDrinkSaleLine { ItemId = 1, DisplayName = "Great Northern", UnitPrice = 6m, Quantity = 1 }],
            "Dylan",
            EightOhTwo);

        Assert.Equal("Great Northern \u00d71", preview.Headline);
        Assert.Equal("$6.00", preview.AmountText);
        Assert.Equal("Added to Dylan", preview.DetailLine);
        Assert.Equal("8:02 PM", preview.TimeText);
    }

    [Fact]
    public void ForDrinks_JoinsMultipleLines_AndSumsAmount()
    {
        var preview = TabUndoPreview.ForDrinks(
            [
                new TabDrinkSaleLine { ItemId = 1, DisplayName = "Great Northern", UnitPrice = 6m, Quantity = 1 },
                new TabDrinkSaleLine { ItemId = 2, DisplayName = "Carlton Draught", UnitPrice = 6m, Quantity = 2 },
            ],
            "Dylan",
            EightOhTwo);

        Assert.Equal("Great Northern \u00d71\nCarlton Draught \u00d72", preview.Headline);
        Assert.Equal("$18.00", preview.AmountText);
        Assert.Equal("Great Northern \u00d71, Carlton Draught \u00d72", preview.Description);
    }

    [Fact]
    public void ForComplimentaryItem_IsNotAPaidSale()
    {
        var preview = TabUndoPreview.ForComplimentaryItem("Water", 1, EightOhTwo);

        Assert.Equal("Free Water \u00d71", preview.Headline);
        Assert.Null(preview.AmountText);
        Assert.Equal("Complimentary member item", preview.DetailLine);
        Assert.Equal(TabUndoPreview.ComplimentaryActionKind, preview.ActionKind);
    }

    [Fact]
    public void ForFunds_ShowsAmountAddedToTab()
    {
        var preview = TabUndoPreview.ForFunds("Cash", 50m, "Dylan", EightOhTwo);

        Assert.Equal("Cash", preview.Headline);
        Assert.Equal("$50.00", preview.AmountText);
        Assert.Equal("Added to Dylan", preview.DetailLine);
        Assert.Equal("Cash $50.00", preview.Description);
    }

    [Fact]
    public void FormatMoney_KeepsCentsAndNegativePrefix()
    {
        Assert.Equal("$6.00", TabUndoPreview.FormatMoney(6m));
        Assert.Equal("-$12.50", TabUndoPreview.FormatMoney(-12.5m));
    }

    [Fact]
    public void Stack_StoresPreview_AndClearsAfterSuccessfulUndo()
    {
        var stack = new TabWorkspaceUndoStack();
        var preview = TabUndoPreview.ForDrinks(
            [new TabDrinkSaleLine { ItemId = 1, DisplayName = "Great Northern", UnitPrice = 6m, Quantity = 1 }],
            "Dylan",
            EightOhTwo);

        stack.PushUndo(preview, () => Task.FromResult(true));

        Assert.True(stack.CanUndo);
        Assert.Equal("Great Northern \u00d71", stack.Preview?.Headline);
        Assert.Equal("Added to Dylan", stack.Preview?.DetailLine);

        Assert.True(stack.TryUndoAsync().GetAwaiter().GetResult());
        Assert.False(stack.CanUndo);
        Assert.Null(stack.Preview);
    }

    [Fact]
    public void Stack_KeepsPreview_WhenUndoFails()
    {
        var stack = new TabWorkspaceUndoStack();
        stack.PushUndo("Undo last drinks (1 line)", () => Task.FromResult(false));

        Assert.False(stack.TryUndoAsync().GetAwaiter().GetResult());
        Assert.True(stack.CanUndo);
        Assert.Equal("Undo last drinks (1 line)", stack.Preview?.Headline);
    }
}
