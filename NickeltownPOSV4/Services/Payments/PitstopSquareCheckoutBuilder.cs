using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Payments;

/// <summary>
/// Builds itemized Square Terminal requests for Pitstop retail so the Square dashboard
/// shows product names, categories, and quantities (not a lump-sum "Pitstop $X").
/// </summary>
public static class PitstopSquareCheckoutBuilder
{
    public static SquarePaymentRequest BuildTerminalRequest(
        IReadOnlyList<PitstopSaleLineCommit> lines,
        decimal chargeTotal,
        decimal cardFee)
    {
        chargeTotal = decimal.Round(chargeTotal, 2, MidpointRounding.AwayFromZero);
        cardFee = decimal.Round(cardFee, 2, MidpointRounding.AwayFromZero);
        if (cardFee < 0m)
        {
            cardFee = 0m;
        }

        var lineItems = new List<SquareTerminalLineItem>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(line.DisplayName) ? "Item" : line.DisplayName.Trim();
            lineItems.Add(new SquareTerminalLineItem
            {
                Name = name,
                Quantity = line.Quantity,
                UnitPrice = decimal.Round(line.UnitPrice, 2, MidpointRounding.AwayFromZero),
                Category = FormatPitstopCategory(line),
            });
        }

        if (cardFee > 0m)
        {
            lineItems.Add(new SquareTerminalLineItem
            {
                Name = "Card Processing Fee",
                Quantity = 1,
                UnitPrice = cardFee,
                Category = "Pitstop",
            });
        }

        if (lineItems.Count == 0)
        {
            var productTotal = decimal.Round(chargeTotal - cardFee, 2, MidpointRounding.AwayFromZero);
            if (productTotal <= 0m)
            {
                productTotal = chargeTotal;
            }

            lineItems.Add(new SquareTerminalLineItem
            {
                Name = "Pitstop sale",
                Quantity = 1,
                UnitPrice = productTotal,
                Category = "Pitstop",
            });
        }

        return new SquarePaymentRequest
        {
            TotalAmount = chargeTotal,
            LineItems = lineItems,
            Note = BuildNote(lines, chargeTotal),
            ReferenceId = $"Pitstop-{DateTime.Now:yyyyMMddHHmmss}",
        };
    }

    internal static string FormatPitstopCategory(PitstopSaleLineCommit line)
    {
        var sub = line.SubCategory?.Trim();
        if (!string.IsNullOrWhiteSpace(sub))
        {
            return $"Pitstop - {sub}";
        }

        var bucket = line.CategoryName?.Trim();
        if (!string.IsNullOrWhiteSpace(bucket)
            && !bucket.Equals("Pitstop", StringComparison.OrdinalIgnoreCase)
            && !bucket.Equals("Shared", StringComparison.OrdinalIgnoreCase)
            && !bucket.Equals("Bar", StringComparison.OrdinalIgnoreCase))
        {
            return $"Pitstop - {bucket}";
        }

        return "Pitstop";
    }

    internal static string BuildNote(IReadOnlyList<PitstopSaleLineCommit> lines, decimal chargeTotal)
    {
        var inv = CultureInfo.InvariantCulture;
        var productLines = lines.Where(l => l.Quantity > 0).ToList();
        if (productLines.Count == 0)
        {
            return $"Pitstop sale ${chargeTotal.ToString("0.00", inv)}";
        }

        var sb = new StringBuilder("Pitstop: ");
        for (var i = 0; i < productLines.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var line = productLines[i];
            var name = string.IsNullOrWhiteSpace(line.DisplayName) ? "Item" : line.DisplayName.Trim();
            sb.Append(name);
            sb.Append(" x");
            sb.Append(line.Quantity.ToString(inv));
        }

        var note = sb.ToString();
        if (note.Length > 500)
        {
            note = note[..497] + "...";
        }

        return note;
    }
}