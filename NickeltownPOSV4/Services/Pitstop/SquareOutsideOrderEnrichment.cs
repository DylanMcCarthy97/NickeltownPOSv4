using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Pitstop;
using NickeltownPOSV4.Models.Settings;
using Square;
using Square.Catalog;
using Square.Orders;

namespace NickeltownPOSV4.Services.Pitstop;

public sealed class SquareOutsideOrderEnrichment
{
    private const int OrderBatchSize = 100;
    private const int CatalogBatchSize = 100;

    private readonly IPitstopCatalogQuery _pitstopCatalog;
    private readonly IItemCatalogQuery _barCatalog;

    public SquareOutsideOrderEnrichment(IPitstopCatalogQuery pitstopCatalog, IItemCatalogQuery barCatalog)
    {
        _pitstopCatalog = pitstopCatalog;
        _barCatalog = barCatalog;
    }

    public async Task<SquarePaymentReconciliationResult> EnrichAsync(
        AppSquareConfig cfg,
        SquarePaymentReconciliationResult result,
        IReadOnlyDictionary<string, string> paymentOrderIds,
        CancellationToken cancellationToken = default)
    {
        if (result.UnmatchedSquarePayments.Count == 0)
        {
            return result;
        }

        var warnings = result.Warnings.ToList();
        var catalogIndex = await SquareClubPosProductCatalogIndex
            .BuildAsync(_pitstopCatalog, _barCatalog, cancellationToken)
            .ConfigureAwait(false);

        var isSandbox = string.Equals(cfg.Environment?.Trim(), "sandbox", StringComparison.OrdinalIgnoreCase);
        var baseUrl = isSandbox ? SquareEnvironment.Sandbox : SquareEnvironment.Production;
        var client = new SquareClient(cfg.AccessToken.Trim(), new ClientOptions { BaseUrl = baseUrl });
        var locationId = cfg.LocationId?.Trim() ?? string.Empty;

        var orderIds = paymentOrderIds.Values
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, Order> ordersById;
        try
        {
            ordersById = await FetchOrdersAsync(client, locationId, orderIds, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not load Square orders for outside terminal sales: {ex.Message}");
            return CopyResult(result, result.UnmatchedSquarePayments, warnings);
        }

        var catalogCategoryNames = await LoadCatalogCategoryNamesAsync(
                client,
                ordersById.Values,
                cancellationToken)
            .ConfigureAwait(false);

        var enrichedPayments = new List<SquareReconciliationPaymentRow>();
        var allLineItems = new List<SquareOrderLineItemRow>();
        var ordersLoaded = 0;
        var ordersMissing = 0;

        foreach (var payment in result.UnmatchedSquarePayments)
        {
            paymentOrderIds.TryGetValue(payment.PaymentId, out var orderId);
            if (string.IsNullOrWhiteSpace(orderId))
            {
                ordersMissing++;
                enrichedPayments.Add(CopyPayment(payment, orderLoadWarning: "Square payment has no linked order."));
                continue;
            }

            if (!ordersById.TryGetValue(orderId.Trim(), out var order))
            {
                ordersMissing++;
                enrichedPayments.Add(CopyPayment(
                    payment,
                    orderId: orderId,
                    orderLoadWarning: "Square order could not be retrieved."));
                continue;
            }

            ordersLoaded++;
            var lineItems = ParseOrderLineItems(order, catalogIndex, catalogCategoryNames);
            allLineItems.AddRange(lineItems);
            enrichedPayments.Add(CopyPayment(
                payment,
                orderId: orderId,
                orderLoaded: true,
                lineItems: lineItems));
        }

        if (ordersMissing > 0)
        {
            warnings.Add($"{ordersMissing} outside-terminal payment(s) could not be matched to a Square order with line items.");
        }

        var unmappedCount = allLineItems.Count(l => !l.MappedToClubPos);
        if (unmappedCount > 0)
        {
            warnings.Add($"{unmappedCount} outside-terminal line item(s) could not be mapped to a ClubPOS product.");
        }

        return CopyResult(
            result,
            enrichedPayments,
            warnings,
            SquareOutsideSalesAggregator.AggregateProducts(allLineItems),
            SquareOutsideSalesAggregator.AggregateCategories(allLineItems),
            ordersLoaded,
            ordersMissing);
    }

    private static async Task<Dictionary<string, Order>> FetchOrdersAsync(
        SquareClient client,
        string locationId,
        IReadOnlyList<string> orderIds,
        CancellationToken cancellationToken)
    {
        var orders = new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < orderIds.Count; i += OrderBatchSize)
        {
            var batch = orderIds.Skip(i).Take(OrderBatchSize).ToList();
            var request = new BatchGetOrdersRequest
            {
                OrderIds = batch,
            };

            if (!string.IsNullOrWhiteSpace(locationId))
            {
                request.LocationId = locationId;
            }

            var response = await client.Orders.BatchGetAsync(request, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (response.Orders is null)
            {
                continue;
            }

            foreach (var order in response.Orders)
            {
                if (order?.Id is not null)
                {
                    orders[order.Id.Trim()] = order;
                }
            }
        }

        return orders;
    }

    private static async Task<IReadOnlyDictionary<string, string>> LoadCatalogCategoryNamesAsync(
        SquareClient client,
        IEnumerable<Order> orders,
        CancellationToken cancellationToken)
    {
        var catalogIds = orders
            .SelectMany(o => o.LineItems ?? Array.Empty<OrderLineItem>())
            .Select(li => li.CatalogObjectId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (catalogIds.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var objectNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var objectToCategoryId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var categoryNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < catalogIds.Count; i += CatalogBatchSize)
        {
            var batch = catalogIds.Skip(i).Take(CatalogBatchSize).ToList();
            var response = await client.Catalog.BatchGetAsync(
                new BatchGetCatalogObjectsRequest
                {
                    ObjectIds = batch,
                    IncludeRelatedObjects = true,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            IndexCatalogObjects(response.Objects, objectNames, objectToCategoryId, categoryNames);
            IndexCatalogObjects(response.RelatedObjects, objectNames, objectToCategoryId, categoryNames);
        }

        var catalogToCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in objectToCategoryId)
        {
            if (categoryNames.TryGetValue(pair.Value, out var categoryName))
            {
                catalogToCategory[pair.Key] = categoryName;
            }
        }

        return catalogToCategory;
    }

    private static void IndexCatalogObjects(
        IEnumerable<CatalogObject>? objects,
        Dictionary<string, string> objectNames,
        Dictionary<string, string> objectToCategoryId,
        Dictionary<string, string> categoryNames)
    {
        if (objects is null)
        {
            return;
        }

        foreach (var obj in objects)
        {
            if (obj is null)
            {
                continue;
            }

            if (obj.TryAsItemVariation(out var variation)
                && !string.IsNullOrWhiteSpace(variation.Id))
            {
                var id = variation.Id.Trim();
                if (variation.ItemVariationData?.Name is string variationName
                    && !string.IsNullOrWhiteSpace(variationName))
                {
                    objectNames[id] = variationName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(variation.ItemVariationData?.ItemId))
                {
                    objectToCategoryId[id] = variation.ItemVariationData.ItemId.Trim();
                }
            }
            else if (obj.TryAsItem(out var item) && !string.IsNullOrWhiteSpace(item.Id))
            {
                var id = item.Id.Trim();
                if (item.ItemData?.Name is string itemName && !string.IsNullOrWhiteSpace(itemName))
                {
                    objectNames[id] = itemName.Trim();
                }

                if (!string.IsNullOrWhiteSpace(item.ItemData?.CategoryId))
                {
                    objectToCategoryId[id] = item.ItemData.CategoryId.Trim();
                }
            }
            else if (obj.TryAsCategory(out var category) && !string.IsNullOrWhiteSpace(category.Id))
            {
                if (category.CategoryData?.Name is string categoryName && !string.IsNullOrWhiteSpace(categoryName))
                {
                    categoryNames[category.Id.Trim()] = categoryName.Trim();
                }
            }
        }

        foreach (var pair in objectToCategoryId.ToList())
        {
            if (categoryNames.ContainsKey(pair.Value))
            {
                continue;
            }

            if (objectNames.TryGetValue(pair.Value, out var parentName))
            {
                categoryNames[pair.Value] = parentName;
            }
        }
    }

    private static List<SquareOrderLineItemRow> ParseOrderLineItems(
        Order order,
        SquareClubPosProductCatalogIndex catalogIndex,
        IReadOnlyDictionary<string, string> catalogCategoryNames)
    {
        var rows = new List<SquareOrderLineItemRow>();
        if (order.LineItems is null)
        {
            return rows;
        }

        foreach (var line in order.LineItems)
        {
            if (line is null)
            {
                continue;
            }

            var qty = ParseQuantity(line.Quantity);
            if (qty <= 0)
            {
                continue;
            }

            var unitPrice = MoneyToDecimal(line.BasePriceMoney) ?? 0m;
            var lineTotal = MoneyToDecimal(line.TotalMoney)
                ?? MoneyToDecimal(line.GrossSalesMoney)
                ?? decimal.Round(unitPrice * qty, 2, MidpointRounding.AwayFromZero);

            var itemName = line.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(itemName) && !string.IsNullOrWhiteSpace(line.VariationName))
            {
                itemName = line.VariationName.Trim();
            }

            var catalogObjectId = line.CatalogObjectId?.Trim();
            string? squareCategory = null;
            if (!string.IsNullOrWhiteSpace(catalogObjectId)
                && catalogCategoryNames.TryGetValue(catalogObjectId, out var categoryName))
            {
                squareCategory = categoryName;
            }

            var mapped = catalogIndex.TryMatch(itemName, line.VariationName);
            var reportCategory = mapped is not null
                ? EventReportCategoryNormalizer.Normalize(mapped.SubCategory, mapped.Name)
                : EventReportCategoryNormalizer.Normalize(squareCategory, itemName);

            rows.Add(new SquareOrderLineItemRow
            {
                ItemName = itemName,
                CategoryName = reportCategory,
                Quantity = qty,
                UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
                LineTotal = decimal.Round(lineTotal, 2, MidpointRounding.AwayFromZero),
                CatalogObjectId = catalogObjectId,
                SquareCategoryName = squareCategory,
                MappedClubPosItemId = mapped?.ItemId,
                MappedClubPosItemName = mapped?.Name,
                MappedToClubPos = mapped is not null,
            });
        }

        return rows;
    }

    private static int ParseQuantity(string? quantity)
    {
        if (string.IsNullOrWhiteSpace(quantity))
        {
            return 0;
        }

        if (decimal.TryParse(quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return (int)decimal.Round(value, 0, MidpointRounding.AwayFromZero);
        }

        return 0;
    }

    private static decimal? MoneyToDecimal(Money? money)
    {
        if (money?.Amount is not long cents)
        {
            return null;
        }

        return decimal.Round(cents / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static SquareReconciliationPaymentRow CopyPayment(
        SquareReconciliationPaymentRow payment,
        string? orderId = null,
        bool orderLoaded = false,
        string? orderLoadWarning = null,
        IReadOnlyList<SquareOrderLineItemRow>? lineItems = null) =>
        new()
        {
            PaymentId = payment.PaymentId,
            OrderId = orderId ?? payment.OrderId,
            PaidAt = payment.PaidAt,
            GrossAmount = payment.GrossAmount,
            ReceiptNumber = payment.ReceiptNumber,
            DeviceName = payment.DeviceName,
            CardLast4 = payment.CardLast4,
            TerminalClass = payment.TerminalClass,
            LocalSaleId = payment.LocalSaleId,
            LocalSaleRef = payment.LocalSaleRef,
            LocalSaleAmount = payment.LocalSaleAmount,
            OrderLoaded = orderLoaded || payment.OrderLoaded,
            OrderLoadWarning = orderLoadWarning ?? payment.OrderLoadWarning,
            LineItems = lineItems ?? payment.LineItems,
        };

    private static SquarePaymentReconciliationResult CopyResult(
        SquarePaymentReconciliationResult result,
        IReadOnlyList<SquareReconciliationPaymentRow> unmatchedPayments,
        List<string> warnings,
        IReadOnlyList<PitstopProductAggregateRow>? outsideProducts = null,
        IReadOnlyList<PitstopCategoryAggregateRow>? outsideCategories = null,
        int ordersLoaded = 0,
        int ordersMissing = 0) =>
        new()
        {
            PosSquareGross = result.PosSquareGross,
            OutsideSquareGross = result.OutsideSquareGross,
            CombinedSquareGross = result.CombinedSquareGross,
            PosTransactionCount = result.PosTransactionCount,
            OutsideTransactionCount = result.OutsideTransactionCount,
            ActualSquareFees = result.ActualSquareFees,
            ExpectedSquareDeposit = result.ExpectedSquareDeposit,
            LoadedFromSquare = result.LoadedFromSquare,
            LoadError = result.LoadError,
            MatchedPayments = result.MatchedPayments,
            UnmatchedSquarePayments = unmatchedPayments,
            MissingLocalPayments = result.MissingLocalPayments,
            OutsideTerminalProductSales = outsideProducts ?? result.OutsideTerminalProductSales,
            OutsideTerminalCategorySales = outsideCategories ?? result.OutsideTerminalCategorySales,
            OutsideOrdersLoadedCount = ordersLoaded,
            OutsideOrdersMissingCount = ordersMissing,
            Warnings = warnings,
        };
}
