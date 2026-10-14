using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Builds shopping-list rows, email body, and PDF from a stock snapshot (no UI dependency).</summary>
public static class ShoppingListEmailComposer
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    public static IReadOnlyList<StockShoppingListRowViewModel> BuildRows(IEnumerable<StockEditorRow> stockRows) =>
        stockRows
            .Where(StockInventoryLevelHelper.IsShoppingListCandidate)
            .Select(r => new StockShoppingListRowViewModel(r))
            .OrderBy(r => r.SortPriority)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string BuildEmailBody(IReadOnlyList<StockShoppingListRowViewModel> rows, bool isTest = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nickeltown Flounderers — Shopping List");
        sb.AppendLine(DateTime.Now.ToString("dddd d MMMM yyyy", Culture));
        if (isTest)
        {
            sb.AppendLine("(Test send from Nickeltown POS Settings)");
        }

        sb.AppendLine();

        if (rows.Count == 0)
        {
            sb.AppendLine("Nothing needs buying right now.");
            sb.AppendLine();
            sb.AppendLine("Sent automatically by Nickeltown POS v4.");
            return sb.ToString();
        }

        var buyNow = rows.Where(r => r.Status is StockVolunteerStatus.OutOfStock or StockVolunteerStatus.BuyNow).ToList();
        var buySoon = rows.Where(r => r.Status == StockVolunteerStatus.BuySoon).ToList();
        var totalNeed = rows.Sum(r => r.NeedQty);
        var exceptions = rows.Count(r => r.HasSetupWarning);

        sb.AppendLine($"Items: {rows.Count}  |  Units needed: {totalNeed}");
        if (exceptions > 0)
        {
            sb.AppendLine($"Exceptions: {exceptions} item(s) need pack size set in product setup.");
        }

        sb.AppendLine();
        AppendSection(sb, "BUY NOW", buyNow);
        AppendSection(sb, "BUY SOON", buySoon);

        sb.AppendLine("A PDF shopping list is attached.");
        sb.AppendLine();
        sb.AppendLine("Sent automatically by Nickeltown POS v4 (Saturday schedule).");
        return sb.ToString();
    }

    public static string BuildEmptyConfirmationBody(bool isTest = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nickeltown Flounderers — Shopping List");
        sb.AppendLine(DateTime.Now.ToString("dddd d MMMM yyyy", Culture));
        if (isTest)
        {
            sb.AppendLine("(Test send from Nickeltown POS Settings)");
        }

        sb.AppendLine();
        sb.AppendLine("Nothing needs buying right now — shopping list is clear.");
        sb.AppendLine();
        sb.AppendLine("Sent automatically by Nickeltown POS v4.");
        return sb.ToString();
    }

    public static byte[] BuildPdf(IReadOnlyList<StockShoppingListRowViewModel> rows) =>
        ShoppingListPdfBuilder.BuildWeeklyCombined(rows);

    private static void AppendSection(StringBuilder sb, string title, IReadOnlyList<StockShoppingListRowViewModel> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        sb.AppendLine(title);
        sb.AppendLine(new string('-', title.Length));
        foreach (var row in rows)
        {
            var merch = row.IsMerch ? " (merch)" : string.Empty;
            sb.AppendLine($"{row.Name}{merch}  [{row.StatusText}]");
            sb.AppendLine($"  Have: {row.HaveQty}   Need: {row.NeedQty}");
            if (!string.IsNullOrEmpty(row.SuggestedDisplayLine))
            {
                sb.AppendLine("  " + row.SuggestedDisplayLine);
            }

            if (!string.IsNullOrEmpty(row.SetupHint))
            {
                sb.AppendLine("  " + row.SetupHint);
            }

            sb.AppendLine();
        }
    }
}
