using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Builds shopping-list email body and PDF for one list kind (Bar or Merchandise).</summary>
public static class ShoppingListEmailComposer
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    public static ShoppingListSnapshot BuildSnapshot(IEnumerable<StockEditorRow> stockRows, ShoppingListKind kind) =>
        ShoppingListSnapshot.Build(stockRows, kind);

    public static string BuildEmailBody(
        ShoppingListSnapshot snapshot,
        bool isTest = false,
        bool isSaturdaySchedule = false)
    {
        var title = ShoppingListCatalog.DisplayTitle(snapshot.Kind);
        var sb = new StringBuilder();
        sb.AppendLine("Nickeltown Flounderers — " + title);
        sb.AppendLine(DateTime.Now.ToString("dddd d MMMM yyyy", Culture));
        if (isTest)
        {
            sb.AppendLine("(Test send from Nickeltown POS Settings)");
        }

        sb.AppendLine();

        if (snapshot.PurchaseRows.Count == 0)
        {
            sb.AppendLine("Nothing needs buying right now.");
            AppendReviewNotes(sb, snapshot);
            sb.AppendLine();
            sb.AppendLine("Sent by Nickeltown POS v4.");
            return sb.ToString();
        }

        sb.AppendLine(
            $"Products to buy: {snapshot.PurchaseRows.Count}  |  Units needed: {snapshot.TotalUnitsToBuy}");
        sb.AppendLine();

        AppendSection(
            sb,
            "BUY NOW",
            snapshot.PurchaseRows.Where(r => r.Status is StockVolunteerStatus.OutOfStock or StockVolunteerStatus.BuyNow).ToList());
        AppendSection(
            sb,
            "BUY SOON",
            snapshot.PurchaseRows.Where(r => r.Status == StockVolunteerStatus.BuySoon).ToList());

        AppendReviewNotes(sb, snapshot);
        sb.AppendLine("A PDF shopping list is attached.");
        sb.AppendLine();
        if (isSaturdaySchedule && snapshot.Kind == ShoppingListKind.Bar)
        {
            sb.AppendLine("Sent automatically by Nickeltown POS v4 (Saturday schedule).");
        }
        else
        {
            sb.AppendLine("Sent from Nickeltown POS v4.");
        }

        return sb.ToString();
    }

    public static string BuildEmptyConfirmationBody(ShoppingListKind kind, bool isTest = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Nickeltown Flounderers — " + ShoppingListCatalog.DisplayTitle(kind));
        sb.AppendLine(DateTime.Now.ToString("dddd d MMMM yyyy", Culture));
        if (isTest)
        {
            sb.AppendLine("(Test send from Nickeltown POS Settings)");
        }

        sb.AppendLine();
        sb.AppendLine("Nothing needs buying right now — shopping list is clear.");
        sb.AppendLine();
        sb.AppendLine("Sent by Nickeltown POS v4.");
        return sb.ToString();
    }

    public static byte[] BuildPdf(ShoppingListSnapshot snapshot) =>
        ShoppingListPdfBuilder.Build(snapshot);

    private static void AppendReviewNotes(StringBuilder sb, ShoppingListSnapshot snapshot)
    {
        if (snapshot.MissingPackSizeNames.Count > 0)
        {
            sb.AppendLine("Pack size needed: " + string.Join(", ", snapshot.MissingPackSizeNames));
        }

        if (snapshot.InvalidReorderNames.Count > 0)
        {
            sb.AppendLine("Review reorder settings: " + string.Join(", ", snapshot.InvalidReorderNames));
        }

        if (snapshot.MissingPackSizeNames.Count > 0 || snapshot.InvalidReorderNames.Count > 0)
        {
            sb.AppendLine();
        }
    }

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
            sb.AppendLine(row.Name);
            var packs = row.PacksToBuy is int p ? p.ToString(CultureInfo.InvariantCulture) : "—";
            sb.AppendLine($"  In stock: {row.HaveQty}   Units to buy: {row.UnitsToBuy}   Packs: {packs}");
            sb.AppendLine();
        }
    }
}
