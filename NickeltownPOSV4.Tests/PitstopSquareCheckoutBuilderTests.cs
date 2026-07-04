using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.Payments;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopSquareCheckoutBuilderTests
{
    [Fact]
    public void BuildTerminalRequest_ItemizesProductsAndFee()
    {
        var lines = new[]
        {
            new PitstopSaleLineCommit
            {
                ItemId = 1,
                DisplayName = "Cola",
                UnitPrice = 5m,
                Quantity = 2,
                CategoryName = "Pitstop",
                SubCategory = "Drinks",
            },
            new PitstopSaleLineCommit
            {
                ItemId = 2,
                DisplayName = "Cap",
                UnitPrice = 20m,
                Quantity = 1,
                CategoryName = "Shared",
                SubCategory = "Merch",
            },
        };

        const decimal cardFee = 0.55m;
        const decimal chargeTotal = 30.55m;
        var request = PitstopSquareCheckoutBuilder.BuildTerminalRequest(lines, chargeTotal, cardFee);

        Assert.Equal(30.55m, request.TotalAmount);
        Assert.Equal(3, request.LineItems.Count);

        Assert.Equal("Cola", request.LineItems[0].Name);
        Assert.Equal(2, request.LineItems[0].Quantity);
        Assert.Equal(5m, request.LineItems[0].UnitPrice);
        Assert.Equal("Pitstop - Drinks", request.LineItems[0].Category);

        Assert.Equal("Cap", request.LineItems[1].Name);
        Assert.Equal("Pitstop - Merch", request.LineItems[1].Category);

        Assert.Equal("Card Processing Fee", request.LineItems[2].Name);
        Assert.Equal(1, request.LineItems[2].Quantity);
        Assert.Equal(0.55m, request.LineItems[2].UnitPrice);
        Assert.Equal("Pitstop", request.LineItems[2].Category);

        Assert.Equal("Pitstop: Cola x2, Cap x1", request.Note);
        Assert.StartsWith("Pitstop-", request.ReferenceId);

        var lineSum = request.LineItems.Sum(i => i.UnitPrice * i.Quantity);
        Assert.Equal(chargeTotal, lineSum);
    }

    [Fact]
    public void BuildTerminalRequest_BucketOnlyCategory_UsesPitstopLabel()
    {
        var lines = new[]
        {
            new PitstopSaleLineCommit
            {
                DisplayName = "Water",
                UnitPrice = 2m,
                Quantity = 1,
                CategoryName = "Pitstop",
            },
        };

        var request = PitstopSquareCheckoutBuilder.BuildTerminalRequest(lines, 2m, 0m);
        Assert.Equal("Pitstop", request.LineItems[0].Category);
        Assert.Equal("Pitstop: Water x1", request.Note);
        Assert.Single(request.LineItems);
    }

    [Fact]
    public void BuildTerminalRequest_LongCart_TruncatesNote()
    {
        var lines = Enumerable.Range(1, 80)
            .Select(i => new PitstopSaleLineCommit
            {
                DisplayName = $"Item{i:000}-ExtraLongNameForNote",
                Quantity = 3,
                UnitPrice = 1m,
            })
            .ToList();

        var request = PitstopSquareCheckoutBuilder.BuildTerminalRequest(lines, 240m, 0m);
        Assert.True(request.Note.Length <= 500);
        Assert.StartsWith("Pitstop: ", request.Note);
        Assert.EndsWith("...", request.Note);
    }
}