using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Shopping list builders and catalog health checklist (split from the main stock VM).</summary>
public sealed partial class StockManagementPageViewModel
{
    public ShoppingListSnapshot BuildBarShoppingListSnapshot() =>
        ShoppingListSnapshot.Build(_all, ShoppingListKind.Bar);

    public ShoppingListSnapshot BuildMerchandiseShoppingListSnapshot() =>
        ShoppingListSnapshot.Build(_all, ShoppingListKind.Merchandise);

    public IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListRegularRows() =>
        BuildBarShoppingListSnapshot().PurchaseRows;

    public IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListMerchRows() =>
        BuildMerchandiseShoppingListSnapshot().PurchaseRows;

    public string BuildShoppingListClipboardText(ShoppingListKind kind)
    {
        var snap = ShoppingListSnapshot.Build(_all, kind);
        var lines = new List<string>
        {
            "Nickeltown Flounderers — " + ShoppingListCatalog.DisplayTitle(kind),
            DateTime.Now.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU")),
            string.Empty,
        };

        if (snap.PurchaseRows.Count == 0)
        {
            lines.Add("Nothing needs buying right now.");
            return string.Join(Environment.NewLine, lines);
        }

        lines.Add($"Products to buy: {snap.PurchaseRows.Count}  |  Units needed: {snap.TotalUnitsToBuy}");
        lines.Add(string.Empty);

        AppendBuySection(lines, "BUY NOW", snap.PurchaseRows.Where(r =>
            r.Status is StockVolunteerStatus.OutOfStock or StockVolunteerStatus.BuyNow).ToList());
        AppendBuySection(lines, "BUY SOON", snap.PurchaseRows.Where(r =>
            r.Status == StockVolunteerStatus.BuySoon).ToList());

        if (snap.MissingPackSizeNames.Count > 0)
        {
            lines.Add("Pack size needed: " + string.Join(", ", snap.MissingPackSizeNames));
        }

        if (snap.InvalidReorderNames.Count > 0)
        {
            lines.Add("Review reorder settings: " + string.Join(", ", snap.InvalidReorderNames));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendBuySection(List<string> lines, string title, IReadOnlyList<StockShoppingListRowViewModel> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        lines.Add(title);
        lines.Add(new string('─', Math.Min(title.Length, 40)));
        foreach (var row in rows)
        {
            lines.Add(row.Name);
            var packs = row.PacksToBuy?.ToString(CultureInfo.InvariantCulture) ?? "—";
            lines.Add($"  In stock: {row.HaveQty}   Units: {row.UnitsToBuy}   Packs: {packs}");
            lines.Add(string.Empty);
        }
    }

    public IReadOnlyList<StockCatalogHealthIssue> BuildCatalogHealthIssues() =>
        StockCatalogHealthService.Evaluate(_all);

    public StockListRowViewModel? FindListRowByItemId(long itemId)
    {
        var inPage = PageRows.FirstOrDefault(r => r.ItemId == itemId);
        if (inPage is not null)
        {
            return inPage;
        }

        var src = _all.FirstOrDefault(r => r.ItemId == itemId);
        return src is null ? null : new StockListRowViewModel(src, isAlternateRow: false);
    }

    public Data.Sqlite.StockEditorRow? GetStockRowById(long itemId) =>
        _all.FirstOrDefault(r => r.ItemId == itemId);
}
