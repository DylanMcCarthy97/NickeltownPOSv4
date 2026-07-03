using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>Builds end-of-day outside lines: shared-catalog merch SKUs plus raffle only (food/drink is Pitstop POS retail).</summary>
public sealed class PitstopOutsideLineCatalogBuilder
{
    public const string LineKindMerchSku = "merch_sku";
    public const string LineKindRaffle = "raffle";
    public const decimal DefaultRaffleUnitPrice = 2m;

    private readonly IPitstopCatalogQuery _catalog;

    public PitstopOutsideLineCatalogBuilder(IPitstopCatalogQuery catalog) => _catalog = catalog;

    public async Task<IReadOnlyList<OutsideItemSaleRow>> BuildOutsideSaleTemplateAsync(CancellationToken cancellationToken = default)
    {
        var products = await _catalog.GetPitstopProductsAsync(null, cancellationToken).ConfigureAwait(false);
        var sharedMerch = products
            .Where(IsSharedMerchProduct)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<OutsideItemSaleRow>(sharedMerch.Count + 1);

        foreach (var row in sharedMerch)
        {
            var merchName = (row.Name ?? string.Empty).Trim();
            if (merchName.Length == 0)
            {
                continue;
            }

            list.Add(new OutsideItemSaleRow
            {
                Key = MerchKey(merchName),
                DisplayLabel = merchName,
                OutsideLineKind = LineKindMerchSku,
                PitstopItemId = row.ItemId,
                SuggestedUnitPrice = ResolveSuggestedUnitPrice(row),
            });
        }

        list.Add(new OutsideItemSaleRow
        {
            Key = "raffle:tickets",
            DisplayLabel = "Raffle tickets",
            OutsideLineKind = LineKindRaffle,
            PitstopItemId = null,
            SuggestedUnitPrice = DefaultRaffleUnitPrice,
        });

        return list;
    }

    public static IReadOnlyList<(long ItemId, string ItemName)> BuildMerchPrizeSeeds(IEnumerable<OutsideItemSaleRow> outsideTemplate) =>
        outsideTemplate
            .Where(r => string.Equals(r.OutsideLineKind, LineKindMerchSku, StringComparison.Ordinal))
            .Select(r => (ItemId: r.PitstopItemId ?? 0L, ItemName: r.DisplayLabel))
            .ToList();

    private static bool IsSharedMerchProduct(PitstopCatalogProductRow product)
    {
        if (!string.Equals(
                StockCatalogTaxonomy.NormalizeBucket(product.CategoryName),
                StockCatalogTaxonomy.BucketShared,
                StringComparison.Ordinal))
        {
            return false;
        }

        var sub = (product.SubCategoryLabel ?? string.Empty).Trim();
        return sub.Contains("merch", StringComparison.OrdinalIgnoreCase)
               || sub.Contains("merchandise", StringComparison.OrdinalIgnoreCase);
    }

    private static string MerchKey(string displayName) =>
        "merch:" + (displayName ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", "_", StringComparison.Ordinal);

    private static decimal? ResolveSuggestedUnitPrice(PitstopCatalogProductRow row)
    {
        var p = (decimal)row.EffectivePitstopPrice;
        return p > 0m ? decimal.Round(p, 2, MidpointRounding.AwayFromZero) : null;
    }
}
