using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Services.AddDrinks;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteTabDrinkSalesService : ITabEntryService
{
    public const string DrinkEntryType = "Drink";

    private readonly SqliteConnectionFactory _factory;

    public SqliteTabDrinkSalesService(SqliteConnectionFactory factory) => _factory = factory;

    public Task<TabDrinkCommitResult> CommitDrinkSaleAsync(
        string tabLegacyId,
        IReadOnlyList<TabDrinkSaleLine> lines,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId))
        {
            return Task.FromResult(TabDrinkCommitResult.Fail("No tab is selected."));
        }

        if (lines.Count == 0)
        {
            return Task.FromResult(TabDrinkCommitResult.Fail("No drinks are selected."));
        }

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                return Task.FromResult(TabDrinkCommitResult.Fail("Each line must have a positive quantity."));
            }
        }

        if (!TabBoardRoute.TryParse(tabLegacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult(TabDrinkCommitResult.Fail("No tab is selected."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var tabPk = conn.QuerySingleOrDefault<long?>(
                new CommandDefinition(
                    """
                    SELECT Id FROM Tabs
                    WHERE IsArchived = 0 AND COALESCE(IsDeleted,0) = 0
                      AND ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                    LIMIT 1
                    """,
                    new { RouteLegacy = routeLegacy, RoutePk = routePk },
                    tx,
                    cancellationToken: cancellationToken));

            if (tabPk is null or 0)
            {
                tx.Rollback();
                return Task.FromResult(TabDrinkCommitResult.Fail("Tab was not found or is archived."));
            }

            var tabPkValue = tabPk.Value;
            var drinkCommitBatchId = Guid.NewGuid().ToString("N");
            var totalCharge = 0m;
            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = conn.QuerySingleOrDefault<ItemStockRow>(
                    new CommandDefinition(
                        """
                        SELECT
                          StockQty,
                          COALESCE(TrackStock, 1) AS TrackStock,
                          COALESCE(OrderInMerchandise, 0) AS OrderInMerchandise,
                          COALESCE(NULLIF(TRIM(CatalogBucket), ''), 'Bar') AS CatalogBucket,
                          COALESCE(NULLIF(TRIM(CatalogSubCategory), ''), 'Drinks') AS CatalogSubCategory,
                          ItemType,
                          Name,
                          ItemDescription
                        FROM Items
                        WHERE Id = @id AND IsActive != 0
                        """,
                        new { id = line.ItemId },
                        tx,
                        cancellationToken: cancellationToken));

                if (row is null)
                {
                    tx.Rollback();
                    return Task.FromResult(TabDrinkCommitResult.Fail($"Item id {line.ItemId} is missing or inactive."));
                }

                var skipStockForSale = StockSaleDeductionHelper.SkipsSoldItemStockDecrement(
                    row.TrackStock,
                    row.OrderInMerchandise,
                    row.CatalogBucket,
                    row.CatalogSubCategory,
                    row.ItemType,
                    row.Name);
                if (!skipStockForSale && row.TrackStock != 0 && row.StockQty < line.Quantity)
                {
                    tx.Rollback();
                    return Task.FromResult(TabDrinkCommitResult.Fail($"Not enough stock for “{line.DisplayName}” (have {row.StockQty}, need {line.Quantity})."));
                }

                var lineTotal = decimal.Round(line.UnitPrice * line.Quantity, 2, MidpointRounding.AwayFromZero);
                totalCharge += lineTotal;

                var legacyEntryId = "v4_e_" + Guid.NewGuid().ToString("N");
                var note = $"{line.DisplayName} × {line.Quantity} @ ${line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture)}";

                var raw = JsonSerializer.Serialize(new
                {
                    line.ItemId,
                    line.DisplayName,
                    line.UnitPrice,
                    line.Quantity,
                    lineTotal,
                    tabLegacyId,
                    drinkCommitBatchId,
                });

                conn.Execute(
                    new CommandDefinition(
                        """
                        INSERT INTO TabEntries (TabId, LegacyEntryId, EntryType, Amount, Note, OccurredAt, RawJson, CreatedAt, CommitBatchId)
                        VALUES (@TabId, @LegacyEntryId, @EntryType, @Amount, @Note, @OccurredAt, @RawJson, datetime('now'), @CommitBatchId)
                        """,
                        new
                        {
                            TabId = tabPkValue,
                            LegacyEntryId = legacyEntryId,
                            EntryType = DrinkEntryType,
                            Amount = lineTotal,
                            Note = note,
                            OccurredAt = stamp,
                            RawJson = raw,
                            CommitBatchId = drinkCommitBatchId,
                        },
                        tx,
                        cancellationToken: cancellationToken));

                var movementRef = $"{tabLegacyId}:{legacyEntryId}";
                if (!skipStockForSale && row.TrackStock != 0)
                {
                    conn.Execute(
                        new CommandDefinition(
                            "UPDATE Items SET StockQty = StockQty - @q, UpdatedAt = datetime('now') WHERE Id = @id",
                            new { q = line.Quantity, id = line.ItemId },
                            tx,
                            cancellationToken: cancellationToken));

                    conn.Execute(
                        new CommandDefinition(
                            """
                            INSERT INTO StockMovements (ItemId, DeltaQty, Reason, Reference, CreatedAt)
                            VALUES (@ItemId, @Delta, @Reason, @Ref, datetime('now'))
                            """,
                            new
                            {
                                ItemId = line.ItemId,
                                Delta = -line.Quantity,
                                Reason = "TabDrinkSale",
                                Ref = movementRef,
                            },
                            tx,
                            cancellationToken: cancellationToken));
                }
                else if (ShotMixerCatalog.IsShotMixerItem(row.Name, row.ItemType))
                {
                    StockSaleDeductionHelper.DeductMixerComponentStock(
                        conn,
                        tx,
                        row.ItemDescription,
                        line.Quantity,
                        movementRef);
                }
            }

            var lastLine = lines[^1];
            var lastSummary = $"{lastLine.DisplayName} × {lastLine.Quantity}";

            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs
                    SET Balance = Balance - @charge,
                        LastDrinkSummary = @last,
                        LastActivityAt = @OccurredAt,
                        UpdatedAt = datetime('now')
                    WHERE Id = @tabId
                    """,
                    new
                    {
                        charge = totalCharge,
                        last = lastSummary,
                        OccurredAt = stamp,
                        tabId = tabPkValue,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            tx.Commit();
            return Task.FromResult(TabDrinkCommitResult.Success(drinkCommitBatchId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabDrinkCommitResult.Fail(ex.Message));
        }
    }

    public Task<TabDrinkCommitResult> ReverseDrinkBatchAsync(
        string tabLegacyId,
        string commitBatchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId))
        {
            return Task.FromResult(TabDrinkCommitResult.Fail("No tab is selected."));
        }

        if (string.IsNullOrWhiteSpace(commitBatchId))
        {
            return Task.FromResult(TabDrinkCommitResult.Fail("Nothing to reverse for that drink sale."));
        }

        var batch = commitBatchId.Trim();

        if (!TabBoardRoute.TryParse(tabLegacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult(TabDrinkCommitResult.Fail("No tab is selected."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var tabPk = conn.QuerySingleOrDefault<long?>(
                new CommandDefinition(
                    """
                    SELECT Id FROM Tabs
                    WHERE IsArchived = 0 AND COALESCE(IsDeleted,0) = 0
                      AND ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                    LIMIT 1
                    """,
                    new { RouteLegacy = routeLegacy, RoutePk = routePk },
                    tx,
                    cancellationToken: cancellationToken));

            if (tabPk is null or 0)
            {
                tx.Rollback();
                return Task.FromResult(TabDrinkCommitResult.Fail("Tab was not found, is archived, or was removed."));
            }

            var tabPkValue = tabPk.Value;
            var rows = conn.Query<DrinkBatchRow>(
                    new CommandDefinition(
                        """
                        SELECT Id, LegacyEntryId, Amount, RawJson
                        FROM TabEntries
                        WHERE TabId = @tabId
                          AND EntryType = @drink
                          AND CommitBatchId = @batch
                        """,
                        new { tabId = tabPkValue, drink = DrinkEntryType, batch },
                        tx,
                        cancellationToken: cancellationToken))
                .AsList();

            if (rows.Count == 0)
            {
                tx.Rollback();
                return Task.FromResult(TabDrinkCommitResult.Fail(
                    "That drink sale was not found (or was recorded before batch undo was available)."));
            }

            var refund = 0m;
            foreach (var row in rows)
            {
                refund += decimal.Round((decimal)row.Amount, 2, MidpointRounding.AwayFromZero);
            }

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(row.LegacyEntryId))
                {
                    tx.Rollback();
                    return Task.FromResult(TabDrinkCommitResult.Fail("Stored drink line is missing an id; undo was cancelled."));
                }

                DrinkSaleRawPayload? payload;
                try
                {
                    payload = JsonSerializer.Deserialize<DrinkSaleRawPayload>(row.RawJson ?? "{}");
                }
                catch (JsonException)
                {
                    tx.Rollback();
                    return Task.FromResult(TabDrinkCommitResult.Fail("Could not read stored drink line data; undo was cancelled."));
                }

                if (payload is null || payload.ItemId <= 0 || payload.Quantity <= 0)
                {
                    tx.Rollback();
                    return Task.FromResult(TabDrinkCommitResult.Fail("Stored drink line data is invalid; undo was cancelled."));
                }

                var itemRow = conn.QuerySingleOrDefault<ItemStockRow>(
                    new CommandDefinition(
                        """
                        SELECT
                          StockQty,
                          COALESCE(TrackStock, 1) AS TrackStock,
                          COALESCE(OrderInMerchandise, 0) AS OrderInMerchandise,
                          COALESCE(NULLIF(TRIM(CatalogBucket), ''), 'Bar') AS CatalogBucket,
                          COALESCE(NULLIF(TRIM(CatalogSubCategory), ''), 'Drinks') AS CatalogSubCategory
                        FROM Items
                        WHERE Id = @id
                        """,
                        new { id = payload.ItemId },
                        tx,
                        cancellationToken: cancellationToken));

                if (itemRow is null)
                {
                    tx.Rollback();
                    return Task.FromResult(TabDrinkCommitResult.Fail($"Item id {payload.ItemId} no longer exists; undo was cancelled."));
                }

                var skipStockForSale = itemRow.OrderInMerchandise != 0
                    || itemRow.TrackStock == 0
                    || StockCatalogTaxonomy.SkipsBarStockDecrement(itemRow.CatalogBucket, itemRow.CatalogSubCategory);
                if (!skipStockForSale && itemRow.TrackStock != 0)
                {
                    conn.Execute(
                        new CommandDefinition(
                            "UPDATE Items SET StockQty = StockQty + @q, UpdatedAt = datetime('now') WHERE Id = @id",
                            new { q = payload.Quantity, id = payload.ItemId },
                            tx,
                            cancellationToken: cancellationToken));

                    var stockRef = $"{tabLegacyId}:{row.LegacyEntryId}";
                    conn.Execute(
                        new CommandDefinition(
                            "DELETE FROM StockMovements WHERE ItemId = @itemId AND Reference = @reference",
                            new { itemId = payload.ItemId, reference = stockRef },
                            tx,
                            cancellationToken: cancellationToken));
                }
            }

            var ids = rows.Select(r => r.Id).ToList();
            conn.Execute(
                new CommandDefinition(
                    "DELETE FROM TabEntries WHERE Id IN @ids",
                    new { ids },
                    tx,
                    cancellationToken: cancellationToken));

            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs
                    SET Balance = Balance + @refund,
                        LastDrinkSummary = COALESCE(
                            (
                                SELECT te2.Note FROM TabEntries te2
                                WHERE te2.TabId = @tabId
                                ORDER BY datetime(te2.OccurredAt) DESC, te2.Id DESC
                                LIMIT 1
                            ),
                            'No drinks yet'),
                        LastActivityAt = @stamp,
                        UpdatedAt = datetime('now')
                    WHERE Id = @tabId
                    """,
                    new
                    {
                        refund,
                        tabId = tabPkValue,
                        stamp,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            tx.Commit();
            return Task.FromResult(TabDrinkCommitResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabDrinkCommitResult.Fail(ex.Message));
        }
    }

    private sealed class DrinkBatchRow
    {
        public long Id { get; set; }

        public string? LegacyEntryId { get; set; }

        public double Amount { get; set; }

        public string? RawJson { get; set; }
    }

    private sealed class DrinkSaleRawPayload
    {
        public long ItemId { get; set; }

        public int Quantity { get; set; }
    }

    private sealed class ItemStockRow
    {
        public int StockQty { get; set; }

        public int TrackStock { get; set; }

        public int OrderInMerchandise { get; set; }

        public string CatalogBucket { get; set; } = StockCatalogTaxonomy.BucketBar;

        public string? ItemType { get; set; }

        public string? Name { get; set; }

        public string? ItemDescription { get; set; }

        public string CatalogSubCategory { get; set; } = "Drinks";
    }
}
