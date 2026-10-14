using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Shopping list builders and catalog health checklist (split from the main stock VM).</summary>
public sealed partial class StockManagementPageViewModel
{
    public IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListRows() =>
        BuildShoppingListRows(includeMerch: true, includeRegular: true);

    public IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListRegularRows() =>
        BuildShoppingListRows(includeMerch: false, includeRegular: true);

    public IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListMerchRows() =>
        BuildShoppingListRows(includeMerch: true, includeRegular: false);

    private IReadOnlyList<StockShoppingListRowViewModel> BuildShoppingListRows(bool includeMerch, bool includeRegular) =>
        _all.Where(StockInventoryLevelHelper.IsShoppingListCandidate)
            .Select(r => new StockShoppingListRowViewModel(r))
            .Where(r => (r.IsMerch && includeMerch) || (!r.IsMerch && includeRegular))
            .OrderBy(r => r.SortPriority)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public string BuildShoppingListClipboardText()
    {
        var regular = BuildShoppingListRegularRows();
        var merch = BuildShoppingListMerchRows();
        var lines = new List<string>
        {
            "Nickeltown Shopping List",
            DateTime.Now.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU")),
            string.Empty,
        };

        if (regular.Count == 0 && merch.Count == 0)
        {
            lines.Add("Nothing needs buying right now.");
            return string.Join(Environment.NewLine, lines);
        }

        var totalItems = regular.Count + merch.Count;
        var totalNeed = regular.Sum(r => r.NeedQty) + merch.Sum(r => r.NeedQty);
        lines.Add($"Items: {totalItems}  |  Units needed: {totalNeed}");
        if (regular.Count > 0 && merch.Count > 0)
        {
            lines.Add($"Bar & supplies: {regular.Count}  |  Merchandise: {merch.Count}");
        }

        lines.Add(string.Empty);
        AppendShoppingListSection(lines, "BAR & SUPPLIES", regular);
        AppendShoppingListSection(lines, "MERCHANDISE", merch);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendShoppingListSection(List<string> lines, string title, IReadOnlyList<StockShoppingListRowViewModel> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        lines.Add(title);
        lines.Add(new string('─', Math.Min(title.Length, 40)));
        foreach (var row in rows)
        {
            lines.Add(row.Name + (string.IsNullOrEmpty(row.StatusText) ? "" : "  [" + row.StatusText + "]"));
            lines.Add($"  Have: {row.HaveQty}   Need: {row.NeedQty}");
            if (!string.IsNullOrEmpty(row.SuggestedDisplayLine))
            {
                lines.Add("  " + row.SuggestedDisplayLine);
            }

            if (!string.IsNullOrEmpty(row.SetupHint))
            {
                lines.Add("  " + row.SetupHint);
            }

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
}
