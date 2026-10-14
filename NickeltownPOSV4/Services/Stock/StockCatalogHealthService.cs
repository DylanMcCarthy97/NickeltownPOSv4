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
    public const string IssueSharedOnePrice = "Shared item needs both prices";
    public const string IssueInactiveWithStock = "Inactive but still has stock";
    public const string IssueDuplicateBarcode = "Duplicate barcode";
    public const string IssueTrackedNoWarn = "Tracked with no warn-below";
    public const string IssueModeFlagDrift = "Stock mode flags out of sync";
    public const string IssueMissingBarcode = "No barcode / SKU";

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

            if (row.IsActive == 0)
            {
                if (row.StockQty > 0 && StockCatalogTaxonomy.MaintainsOnHandQuantity(mode))
                {
                    issues.Add(new StockCatalogHealthIssue(
                        row.ItemId,
                        row.Name,
                        IssueInactiveWithStock,
                        $"{row.StockQty} still on hand while inactive."));
                }

                continue;
            }

            if (row.ShowInBar != 0 && row.UsesOpenPrice == 0 && !(row.BarPrice is > 0.00001d))
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueMissingChannelPrice,
                    "Shown in Bar but has no Bar price (and not open-price)."));
            }

            if (row.ShowInPitstop != 0 && row.UsesOpenPrice == 0 && !(row.PitstopPrice is > 0.00001d)
                && !(bucket == StockCatalogTaxonomy.BucketShared && row.BarPrice is > 0.00001d))
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueMissingChannelPrice,
                    "Shown in Pitstop but has no Pitstop price (and not open-price)."));
            }

            if (bucket == StockCatalogTaxonomy.BucketShared
                && row.UsesOpenPrice == 0
                && ((row.BarPrice is > 0.00001d) ^ (row.PitstopPrice is > 0.00001d)))
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueSharedOnePrice,
                    "Shared items should set both Bar and Pitstop prices when possible."));
            }

            if (StockCatalogTaxonomy.MaintainsOnHandQuantity(mode)
                && StockInventoryLevelHelper.ResolveWarnBelow(row) is null)
            {
                issues.Add(new StockCatalogHealthIssue(
                    row.ItemId,
                    row.Name,
                    IssueTrackedNoWarn,
                    "Tracked item has no Warn Me Below — shopping list may never flag it."));
            }

            if (string.IsNullOrWhiteSpace(row.Sku)
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
