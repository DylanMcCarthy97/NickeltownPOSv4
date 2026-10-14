using System;
using System.Collections.Generic;
using System.Linq;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services.AddDrinks;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Catalog data-quality checks for stock admin (volunteer-facing checklist).</summary>
public static class StockCatalogHealthService
{
    public const string IssueMissingChannelPrice = "Missing channel price";
    public const string IssueInactiveWithStock = "Inactive but still has stock";
    public const string IssueDuplicateBarcode = "Duplicate barcode";
    public const string IssueTrackedNoWarn = "Tracked with no warn-below";
    public const string IssueModeFlagDrift = "Stock mode flags out of sync";
    public const string IssueMissingBarcode = "No barcode / SKU";
    public const string IssueFoodTracked = "Food should not track stock";

    public static IReadOnlyList<StockCatalogHealthIssue> Evaluate(IReadOnlyList<StockEditorRow> rows)
    {
        var issues = new List<StockCatalogHealthIssue>();
        if (rows.Count == 0)
        {
            return issues;
        }

        var barcodeOwners = new Dictionary<string, List<StockEditorRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            CollectBarcode(barcodeOwners, row.Sku, row);
            foreach (var alt in StockAlternateSkuHelper.ParseAlternateSkuList(row.AlternateSkusJson))
            {
                CollectBarcode(barcodeOwners, alt, row);
            }
        }

        foreach (var (code, owners) in barcodeOwners)
        {
            var distinct = owners.GroupBy(o => o.ItemId).Select(g => g.First()).ToList();
            if (distinct.Count < 2)
            {
                continue;
            }

            foreach (var owner in distinct)
            {
                issues.Add(new StockCatalogHealthIssue(
                    owner.ItemId,
                    owner.Name,
                    IssueDuplicateBarcode,
                    $"Barcode \"{code}\" is also on: "
                    + string.Join(", ", distinct.Where(d => d.ItemId != owner.ItemId).Select(d => d.Name))));
            }
        }

        foreach (var row in rows)
        {
            if (ShotMixerCatalog.IsShotMixerItem(row.Name, row.ItemType))
            {
                continue;
            }

            var bucket = StockCatalogTaxonomy.NormalizeBucket(row.CatalogBucket);
            var sub = StockCatalogTaxonomy.NormalizeSubCategory(bucket, row.CatalogSubCategory);
            var isFood = StockCatalogTaxonomy.IsFoodSubCategory(bucket, sub);
            var isMerch = StockCatalogTaxonomy.IsMerchSubCategory(bucket, sub);
            var mode = string.IsNullOrWhiteSpace(row.StockMode)
                ? StockCatalogTaxonomy.StockModeFromLegacyFlags(row.TrackStock, row.OrderInMerchandise, row.NotGonnaOrderBack)
                : StockCatalogTaxonomy.NormalizeStockMode(row.StockMode);

            StockCatalogTaxonomy.ApplyStockModeToFlags(mode, out var expectTrack, out var expectOrderIn, out var expectNgb, out _, out var expectRunOut);
            if (row.TrackStock != expectTrack
                || row.OrderInMerchandise != expectOrderIn
                || row.NotGonnaOrderBack != expectNgb
                || row.IsRunOutItem != expectRunOut)
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueModeFlagDrift,
                    $"Mode is {mode} but Track/OrderIn/SellUntilGone flags do not match. Save the item to repair."));
            }

            if (isFood && StockCatalogTaxonomy.MaintainsOnHandQuantity(mode))
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueFoodTracked,
                    "Hot food is not counted — set stock mode to Not tracked."));
            }

            if (row.IsActive == 0)
            {
                if (row.StockQty > 0 && StockCatalogTaxonomy.MaintainsOnHandQuantity(mode) && !isFood)
                {
                    issues.Add(new StockCatalogHealthIssue(
                        row.ItemId,
                        row.Name,
                        IssueInactiveWithStock,
                        $"{row.StockQty} still on hand while inactive."));
                }

                continue;
            }

            // Bar price may be 0.00 (free at bar) or omitted when Pitstop has the paid price.
            // Only flag when there is no Bar price row, not open-price, and no Pitstop price either.
            var hasBarPriceRow = row.BarPrice is not null;
            var hasPitstopPrice = row.PitstopPrice is > 0.00001d;
            if (row.ShowInBar != 0
                && row.UsesOpenPrice == 0
                && !hasBarPriceRow
                && !hasPitstopPrice)
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueMissingChannelPrice,
                    "Shown in Bar but has no Bar price (set 0.00 for free at bar, or add a Pitstop price)."));
            }

            // Shared items may sell in Pitstop using the Bar price fallback — only flag when neither price exists.
            // Free-at-bar (0.00) does not count as a Pitstop fallback.
            if (row.ShowInPitstop != 0 && row.UsesOpenPrice == 0
                && !hasPitstopPrice
                && !(bucket == StockCatalogTaxonomy.BucketShared && row.BarPrice is > 0.00001d))
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueMissingChannelPrice,
                    "Shown in Pitstop but has no Pitstop price (and not open-price)."));
            }

            if (!isFood
                && StockCatalogTaxonomy.MaintainsOnHandQuantity(mode)
                && StockInventoryLevelHelper.ResolveWarnBelow(row) is null)
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueTrackedNoWarn,
                    "Tracked item has no Warn Me Below — shopping list may never flag it."));
            }

            // Merch and Food are sold by name on the till — barcodes are optional.
            if (!isMerch
                && !isFood
                && string.IsNullOrWhiteSpace(row.Sku)
                && !StockAlternateSkuHelper.ParseAlternateSkuList(row.AlternateSkusJson).Any())
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueMissingBarcode,
                    "No primary SKU or alternate barcodes — scanners cannot find this item."));
            }
        }

        return issues
            .OrderBy(i => i.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<StockCatalogHealthSummaryRow> Summarize(IReadOnlyList<StockCatalogHealthIssue> issues) =>
        issues
            .GroupBy(i => i.Kind)
            .Select(g => new StockCatalogHealthSummaryRow(g.Key, g.Count()))
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.Kind, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void CollectBarcode(
        Dictionary<string, List<StockEditorRow>> map,
        string? code,
        StockEditorRow row)
    {
        var t = (code ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            return;
        }

        if (!map.TryGetValue(t, out var list))
        {
            list = [];
            map[t] = list;
        }

        list.Add(row);
    }
}

public sealed record StockCatalogHealthIssue(long ItemId, string ItemName, string Kind, string Detail);

public sealed record StockCatalogHealthSummaryRow(string Kind, int Count);
