using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteStockEditingService : IStockEditingService
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteStockEditingService(SqliteConnectionFactory factory) => _factory = factory;

    public Task<IReadOnlyList<StockCategoryRow>> GetStockCategoriesAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<StockCategoryRow>(
            new CommandDefinition(
                """
                SELECT Id, Name, SortOrder
                FROM Categories
                WHERE COALESCE(IsActive, 1) != 0
                ORDER BY SortOrder, Name COLLATE NOCASE
                """,
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<StockCategoryRow>>(rows.AsList());
    }

    public Task<StockCategoryRow> CreateStockCategoryAsync(string name, CancellationToken cancellationToken = default)
    {
        var label = (name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(label))
        {
            throw new ArgumentException("Category name is required.");
        }

        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        var dup = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*) FROM Categories
                WHERE COALESCE(IsActive,1) != 0
                  AND lower(trim(Name)) = lower(trim(@n))
                """,
                new { n = label },
                tx,
                cancellationToken: cancellationToken));
        if (dup > 0)
        {
            tx.Rollback();
            throw new InvalidOperationException("A category with that name already exists.");
        }

        var sort = conn.ExecuteScalar<long?>(
            new CommandDefinition(
                "SELECT MAX(SortOrder) FROM Categories",
                transaction: tx,
                cancellationToken: cancellationToken)) ?? 0;
        var legacyId = "v4c_" + Guid.NewGuid().ToString("N");
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO Categories (LegacyId, LegacyKey, Name, SortOrder, IsActive, CreatedAt, UpdatedAt)
                VALUES (@LegacyId, @LegacyKey, @Name, @SortOrder, 1, datetime('now'), datetime('now'))
                """,
                new
                {
                    LegacyId = legacyId,
                    LegacyKey = legacyId,
                    Name = label,
                    SortOrder = sort + 1,
                },
                tx,
                cancellationToken: cancellationToken));

        var id = conn.QuerySingle<long>(
            new CommandDefinition(
                "SELECT Id FROM Categories WHERE LegacyId = @l",
                new { l = legacyId },
                tx,
                cancellationToken: cancellationToken));

        tx.Commit();
        return Task.FromResult(new StockCategoryRow { Id = id, Name = label, SortOrder = (int)(sort + 1) });
    }

    public Task UpdateStockCategoryAsync(long categoryId, string newName, CancellationToken cancellationToken = default)
    {
        var label = (newName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(label))
        {
            throw new ArgumentException("Category name is required.");
        }

        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        var active = conn.ExecuteScalar<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Categories WHERE Id = @id AND COALESCE(IsActive,1) != 0",
                new { id = categoryId },
                tx,
                cancellationToken: cancellationToken));
        if (active == 0)
        {
            tx.Rollback();
            throw new InvalidOperationException("That category does not exist or is inactive.");
        }

        var dup = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*) FROM Categories
                WHERE COALESCE(IsActive,1) != 0
                  AND Id != @Id
                  AND lower(trim(Name)) = lower(trim(@n))
                """,
                new { Id = categoryId, n = label },
                tx,
                cancellationToken: cancellationToken));
        if (dup > 0)
        {
            tx.Rollback();
            throw new InvalidOperationException("A category with that name already exists.");
        }

        conn.Execute(
            new CommandDefinition(
                """
                UPDATE Categories
                SET Name = @n, UpdatedAt = datetime('now')
                WHERE Id = @id
                """,
                new { n = label, id = categoryId },
                tx,
                cancellationToken: cancellationToken));

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task MoveStockCategoryAsync(long categoryId, int direction, CancellationToken cancellationToken = default)
    {
        if (direction == 0)
        {
            return Task.CompletedTask;
        }

        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        var rows = conn.Query<(long Id, int SortOrder)>(
            new CommandDefinition(
                """
                SELECT Id, SortOrder
                FROM Categories
                WHERE COALESCE(IsActive,1) != 0
                ORDER BY SortOrder, Name COLLATE NOCASE
                """,
                transaction: tx,
                cancellationToken: cancellationToken)).AsList();

        var i = rows.FindIndex(r => r.Id == categoryId);
        if (i < 0)
        {
            tx.Rollback();
            return Task.CompletedTask;
        }

        var j = i + (direction < 0 ? -1 : 1);
        if (j < 0 || j >= rows.Count)
        {
            tx.Rollback();
            return Task.CompletedTask;
        }

        var sortI = rows[i].SortOrder;
        var sortJ = rows[j].SortOrder;
        conn.Execute(
            new CommandDefinition(
                "UPDATE Categories SET SortOrder = @s, UpdatedAt = datetime('now') WHERE Id = @id",
                new { s = sortJ, id = rows[i].Id },
                tx,
                cancellationToken: cancellationToken));
        conn.Execute(
            new CommandDefinition(
                "UPDATE Categories SET SortOrder = @s, UpdatedAt = datetime('now') WHERE Id = @id",
                new { s = sortI, id = rows[j].Id },
                tx,
                cancellationToken: cancellationToken));

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task SoftDeleteStockCategoryAsync(long categoryId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        var active = conn.ExecuteScalar<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Categories WHERE Id = @id AND COALESCE(IsActive,1) != 0",
                new { id = categoryId },
                tx,
                cancellationToken: cancellationToken));
        if (active == 0)
        {
            tx.Rollback();
            return Task.CompletedTask;
        }

        conn.Execute(
            new CommandDefinition(
                "UPDATE Items SET CategoryId = NULL, UpdatedAt = datetime('now') WHERE CategoryId = @id",
                new { id = categoryId },
                tx,
                cancellationToken: cancellationToken));

        conn.Execute(
            new CommandDefinition(
                """
                UPDATE Categories
                SET IsActive = 0, UpdatedAt = datetime('now')
                WHERE Id = @id
                """,
                new { id = categoryId },
                tx,
                cancellationToken: cancellationToken));

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<StockEditorRow>> GetStockRowsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<StockEditorRow>(
            new CommandDefinition(
                """
                SELECT
                  i.Id AS ItemId,
                  i.Name,
                  i.Sku,
                  i.ItemType,
                  i.StockQty,
                  COALESCE(i.TrackStock, 1) AS TrackStock,
                  i.LegacyId,
                  i.CategoryId AS CategoryId,
                  trim(COALESCE(NULLIF(TRIM(i.CatalogBucket), ''), 'Bar'))
                    || ' / '
                    || trim(COALESCE(NULLIF(TRIM(i.CatalogSubCategory), ''), 'Drinks')) AS CategoryName,
                  COALESCE(NULLIF(TRIM(i.CatalogBucket), ''), 'Bar') AS CatalogBucket,
                  COALESCE(NULLIF(TRIM(i.CatalogSubCategory), ''), 'Drinks') AS CatalogSubCategory,
                  COALESCE(NULLIF(TRIM(i.StockMode), ''), 'Tracked') AS StockMode,
                  COALESCE(i.NotGonnaOrderBack, 0) AS NotGonnaOrderBack,
                  COALESCE(i.IncludeInWeeklyStockReport, 1) AS IncludeInWeeklyStockReport,
                  COALESCE(i.IsRunOutItem, 0) AS IsRunOutItem,
                  COALESCE(i.IsSharedItem, 0) AS IsSharedItem,
                  COALESCE(i.IsOnSpecial, 0) AS IsOnSpecial,
                  i.SpecialType AS SpecialType,
                  i.SpecialValue AS SpecialValue,
                  i.SpecialLabel AS SpecialLabel,
                  i.SpecialAppliesTo AS SpecialAppliesTo,
                  i.AlternateSkusJson AS AlternateSkusJson,
                  i.ItemDescription AS ItemDescription,
                  i.ImagePath,
                  i.RawJson,
                  COALESCE(i.IsActive, 1) AS IsActive,
                  (
                    SELECT ipb.Price
                    FROM ItemPrices ipb
                    WHERE ipb.ItemId = i.Id AND COALESCE(ipb.PriceKind, 'Bar') = 'Bar'
                    ORDER BY datetime(ipb.EffectiveFrom) DESC, ipb.Id DESC
                    LIMIT 1
                  ) AS BarPrice,
                  (
                    SELECT ipg.Price
                    FROM ItemPrices ipg
                    WHERE ipg.ItemId = i.Id AND lower(trim(COALESCE(ipg.PriceKind, ''))) = 'guest'
                    ORDER BY datetime(ipg.EffectiveFrom) DESC, ipg.Id DESC
                    LIMIT 1
                  ) AS GuestPrice,
                  (
                    SELECT ipp.Price
                    FROM ItemPrices ipp
                    WHERE ipp.ItemId = i.Id AND lower(trim(COALESCE(ipp.PriceKind, ''))) = 'pitstop'
                    ORDER BY datetime(ipp.EffectiveFrom) DESC, ipp.Id DESC
                    LIMIT 1
                  ) AS PitstopPrice,
                  (
                    SELECT ips.Price
                    FROM ItemPrices ips
                    WHERE ips.ItemId = i.Id AND lower(trim(COALESCE(ips.PriceKind, ''))) = 'barspecial'
                    ORDER BY datetime(ips.EffectiveFrom) DESC, ips.Id DESC
                    LIMIT 1
                  ) AS BarSpecialPrice,
                  (
                    SELECT ipgs.Price
                    FROM ItemPrices ipgs
                    WHERE ipgs.ItemId = i.Id AND lower(trim(COALESCE(ipgs.PriceKind, ''))) = 'guestspecial'
                    ORDER BY datetime(ipgs.EffectiveFrom) DESC, ipgs.Id DESC
                    LIMIT 1
                  ) AS GuestSpecialPrice,
                  (
                    SELECT ipps.Price
                    FROM ItemPrices ipps
                    WHERE ipps.ItemId = i.Id AND lower(trim(COALESCE(ipps.PriceKind, ''))) = 'pitstopspecial'
                    ORDER BY datetime(ipps.EffectiveFrom) DESC, ipps.Id DESC
                    LIMIT 1
                  ) AS PitstopSpecialPrice,
                  i.CostPrice AS CostPrice,
                  i.LowStockThreshold AS LowStockThreshold,
                  COALESCE(i.UsesOpenPrice, 0) AS UsesOpenPrice,
                  COALESCE(i.OrderInMerchandise, 0) AS OrderInMerchandise,
                  COALESCE(i.ShowInBar, 1) AS ShowInBar,
                  COALESCE(i.ShowInPitstop, 0) AS ShowInPitstop,
                  i.PreferredStockLevel AS PreferredStockLevel,
                  i.WarnMeBelow AS WarnMeBelow,
                  i.PurchaseUnitQty AS PurchaseUnitQty,
                  i.ShowOnShoppingList AS ShowOnShoppingList,
                  i.LastStockCountDate AS LastStockCountDate
                FROM Items i
                WHERE (@IncludeInactive != 0 OR COALESCE(i.IsActive, 1) != 0)
                ORDER BY
                  lower(trim(COALESCE(i.CatalogBucket, ''))) COLLATE NOCASE,
                  lower(trim(COALESCE(i.CatalogSubCategory, ''))) COLLATE NOCASE,
                  i.Name COLLATE NOCASE
                """,
                new { IncludeInactive = includeInactive ? 1 : 0 },
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<StockEditorRow>>(rows.AsList());
    }

    public Task<int> CountOtherItemsWithSkuAsync(long excludeItemId, string sku, CancellationToken cancellationToken = default)
    {
        var needle = (sku ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(needle))
        {
            return Task.FromResult(0);
        }

        using var conn = _factory.OpenConnection();
        var n = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(*)
                FROM Items i
                WHERE COALESCE(i.IsActive, 1) != 0
                  AND i.Id != @ExcludeId
                  AND lower(trim(COALESCE(i.Sku, ''))) = lower(trim(@Sku))
                """,
                new { ExcludeId = excludeItemId, Sku = needle },
                cancellationToken: cancellationToken));
        return Task.FromResult(n);
    }

    public Task<int> CountOtherItemsMatchingScanCodeAsync(long excludeItemId, string scanCode, CancellationToken cancellationToken = default)
    {
        var code = (scanCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return Task.FromResult(0);
        }

        using var conn = _factory.OpenConnection();
        var rows = conn.Query<(long Id, string? Sku, string? AltJson)>(
            new CommandDefinition(
                """
                SELECT Id, Sku, AlternateSkusJson
                FROM Items
                WHERE COALESCE(IsActive, 1) != 0
                  AND Id != @ExcludeId
                """,
                new { ExcludeId = excludeItemId },
                cancellationToken: cancellationToken)).AsList();

        var n = 0;
        foreach (var r in rows)
        {
            if (string.Equals((r.Sku ?? string.Empty).Trim(), code, StringComparison.OrdinalIgnoreCase))
            {
                n++;
                continue;
            }

            foreach (var alt in ParseAlternateSkus(r.AltJson))
            {
                if (string.Equals(alt, code, StringComparison.OrdinalIgnoreCase))
                {
                    n++;
                    break;
                }
            }
        }

        return Task.FromResult(n);
    }

    private static IEnumerable<string> ParseAlternateSkus(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            yield break;
        }

        List<string>? list = null;
        try
        {
            list = JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            yield break;
        }

        if (list is null)
        {
            yield break;
        }

        foreach (var s in list)
        {
            var t = (s ?? string.Empty).Trim();
            if (t.Length > 0)
            {
                yield return t;
            }
        }
    }

    public Task<long> CreateStockItemAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        var legacyId = "v4i_" + Guid.NewGuid().ToString("N");
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO Items (
                  LegacyId, LegacyKey, Name, Sku, CategoryId, ItemType, StockQty, TrackStock,
                  ImagePath, RawJson, IsActive, CatalogBucket, CatalogSubCategory, StockMode,
                  ShowInBar, ShowInPitstop, IncludeInWeeklyStockReport, CreatedAt, UpdatedAt)
                VALUES (
                  @LegacyId, @LegacyKey, @Name, NULL, NULL, 'Item', 0, 1,
                  NULL, NULL, 1, @Bucket, @Sub, @StockMode,
                  1, 0, 1, datetime('now'), datetime('now'))
                """,
                new
                {
                    LegacyId = legacyId,
                    LegacyKey = legacyId,
                    Name = "New item",
                    Bucket = StockCatalogTaxonomy.BucketBar,
                    Sub = StockCatalogTaxonomy.DefaultSubCategory(StockCatalogTaxonomy.BucketBar),
                    StockMode = StockCatalogTaxonomy.StockModeTracked,
                },
                tx,
                cancellationToken: cancellationToken));

        var id = conn.QuerySingle<long>(
            new CommandDefinition(
                "SELECT Id FROM Items WHERE LegacyId = @l",
                new { l = legacyId },
                tx,
                cancellationToken: cancellationToken));

        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO ItemPrices (ItemId, Price, EffectiveFrom, CreatedAt, PriceKind)
                VALUES (@ItemId, @Price, datetime('now'), datetime('now'), @Kind)
                """,
                new { ItemId = id, Price = 0m, Kind = "Bar" },
                tx,
                cancellationToken: cancellationToken));

        tx.Commit();
        return Task.FromResult(id);
    }

    public Task UpsertLatestItemPriceAsync(long itemId, string priceKind, decimal price, CancellationToken cancellationToken = default)
    {
        var kind = string.IsNullOrWhiteSpace(priceKind) ? "Bar" : priceKind.Trim();
        using var conn = _factory.OpenConnection();
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO ItemPrices (ItemId, Price, EffectiveFrom, CreatedAt, PriceKind)
                VALUES (@ItemId, @Price, datetime('now'), datetime('now'), @Kind)
                """,
                new { ItemId = itemId, Price = price, Kind = kind },
                cancellationToken: cancellationToken));
        return Task.CompletedTask;
    }

    public Task UpdateStockRowAsync(
        long itemId,
        int newQty,
        int trackStock,
        long? categoryId,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        var oldQty = conn.QuerySingleOrDefault<int?>(
            new CommandDefinition(
                "SELECT StockQty FROM Items WHERE Id = @id",
                new { id = itemId },
                tx,
                cancellationToken: cancellationToken));

        if (oldQty is null)
        {
            tx.Rollback();
            return Task.CompletedTask;
        }

        if (categoryId is long cid)
        {
            var exists = conn.ExecuteScalar<long>(
                new CommandDefinition(
                    "SELECT COUNT(*) FROM Categories WHERE Id = @id AND COALESCE(IsActive,1) != 0",
                    new { id = cid },
                    tx,
                    cancellationToken: cancellationToken));
            if (exists == 0)
            {
                tx.Rollback();
                throw new InvalidOperationException("That category no longer exists.");
            }
        }

        conn.Execute(
            new CommandDefinition(
                """
                UPDATE Items
                SET StockQty = @q,
                    TrackStock = @tr,
                    CategoryId = @cat,
                    UpdatedAt = datetime('now')
                WHERE Id = @id
                """,
                new
                {
                    q = newQty,
                    tr = trackStock,
                    cat = categoryId,
                    id = itemId,
                },
                tx,
                cancellationToken: cancellationToken));

        var delta = newQty - oldQty.Value;
        if (delta != 0)
        {
            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO StockMovements (ItemId, DeltaQty, Reason, Reference, CreatedAt)
                    VALUES (@ItemId, @Delta, @Reason, @Ref, datetime('now'))
                    """,
                    new
                    {
                        ItemId = itemId,
                        Delta = delta,
                        Reason = "StockEditor",
                        Ref = "manual_edit",
                    },
                    tx,
                    cancellationToken: cancellationToken));
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task PermanentlyDeleteStockItemAsync(long itemId, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        conn.Execute(
            new CommandDefinition(
                "DELETE FROM Items WHERE Id = @id",
                new { id = itemId },
                cancellationToken: cancellationToken));
        return Task.CompletedTask;
    }

    public Task UpdateItemAdminAsync(StockItemAdminWrite write, CancellationToken cancellationToken = default)
    {
        var label = (write.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(label))
        {
            throw new ArgumentException("Item name is required.");
        }

        if (write.StockQty < 0)
        {
            throw new ArgumentException("Stock cannot be negative.");
        }

        var bucket = StockCatalogTaxonomy.NormalizeBucket(write.CatalogBucket);
        var sub = StockCatalogTaxonomy.NormalizeSubCategory(bucket, write.CatalogSubCategory);
        var active = write.IsActive != 0 ? 1 : 0;
        var movementRef = string.IsNullOrWhiteSpace(write.StockMovementReference) ? "admin_edit" : write.StockMovementReference.Trim();

        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        var oldQty = conn.QuerySingleOrDefault<int?>(
            new CommandDefinition(
                "SELECT StockQty FROM Items WHERE Id = @id",
                new { id = write.ItemId },
                tx,
                cancellationToken: cancellationToken));

        if (oldQty is null)
        {
            tx.Rollback();
            return Task.CompletedTask;
        }

        var img = string.IsNullOrWhiteSpace(write.ImagePath) ? null : write.ImagePath.Trim();
        var skuVal = string.IsNullOrWhiteSpace(write.Sku) ? null : write.Sku.Trim();
        var openPx = write.UsesOpenPrice != 0 ? 1 : 0;
        var showPit = write.ShowInPitstop != 0 ? 1 : 0;
        var showBarVal = write.ShowInBar != 0 ? 1 : 0;
        var orderIn = write.OrderInMerchandise != 0 ? 1 : 0;
        var desc = string.IsNullOrWhiteSpace(write.ItemDescription) ? null : write.ItemDescription.Trim();
        var altJson = string.IsNullOrWhiteSpace(write.AlternateSkusJson) ? null : write.AlternateSkusJson.Trim();

        conn.Execute(
            new CommandDefinition(
                """
                UPDATE Items
                SET Name = @name,
                    Sku = @sku,
                    StockQty = @q,
                    TrackStock = @tr,
                    CategoryId = @catId,
                    ImagePath = @img,
                    IsActive = @active,
                    CostPrice = @cost,
                    LowStockThreshold = @lowTh,
                    UsesOpenPrice = @openPx,
                    ShowInPitstop = @showPit,
                    ShowInBar = @showBarVal,
                    OrderInMerchandise = @orderIn,
                    CatalogBucket = @bucket,
                    CatalogSubCategory = @sub,
                    StockMode = @stockMode,
                    NotGonnaOrderBack = @ngb,
                    IncludeInWeeklyStockReport = @incWeekly,
                    IsRunOutItem = @runOut,
                    IsSharedItem = @shared,
                    IsOnSpecial = @onSpec,
                    SpecialType = @specialType,
                    SpecialValue = @specialValue,
                    SpecialLabel = @specialLabel,
                    SpecialAppliesTo = @specialAppliesTo,
                    AlternateSkusJson = @altJson,
                    ItemDescription = @desc,
                    PreferredStockLevel = @pref,
                    WarnMeBelow = @warn,
                    PurchaseUnitQty = @pack,
                    ShowOnShoppingList = COALESCE(@shopList, ShowOnShoppingList),
                    LastStockCountDate = COALESCE(@lastCount, LastStockCountDate),
                    UpdatedAt = datetime('now')
                WHERE Id = @id
                """,
                new
                {
                    name = label,
                    sku = skuVal,
                    q = write.StockQty,
                    tr = write.TrackStock,
                    img,
                    active,
                    cost = write.CostPrice,
                    lowTh = write.LowStockThreshold,
                    openPx,
                    showPit,
                    showBarVal,
                    orderIn,
                    bucket,
                    sub,
                    stockMode = (write.StockMode ?? StockCatalogTaxonomy.StockModeTracked).Trim(),
                    ngb = write.NotGonnaOrderBack,
                    incWeekly = write.IncludeInWeeklyStockReport,
                    runOut = write.IsRunOutItem,
                    shared = write.IsSharedItem,
                    onSpec = write.IsOnSpecial,
                    specialType = string.IsNullOrWhiteSpace(write.SpecialType) ? null : write.SpecialType.Trim(),
                    specialValue = string.IsNullOrWhiteSpace(write.SpecialValue) ? null : write.SpecialValue.Trim(),
                    specialLabel = string.IsNullOrWhiteSpace(write.SpecialLabel) ? null : write.SpecialLabel.Trim(),
                    specialAppliesTo = string.IsNullOrWhiteSpace(write.SpecialAppliesTo) ? null : write.SpecialAppliesTo.Trim(),
                    altJson,
                    desc,
                    pref = write.PreferredStockLevel,
                    warn = write.WarnMeBelow,
                    pack = write.PurchaseUnitQty,
                    shopList = write.ShowOnShoppingList,
                    lastCount = write.LastStockCountDate,
                    catId = write.CategoryId,
                    id = write.ItemId,
                },
                tx,
                cancellationToken: cancellationToken));

        var delta = write.StockQty - oldQty.Value;
        if (delta != 0)
        {
            var adjustmentReason = string.IsNullOrWhiteSpace(movementRef) ? "StockAdmin" : movementRef.Trim();
            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO StockMovements (ItemId, DeltaQty, Reason, Reference, CreatedAt)
                    VALUES (@ItemId, @Delta, @Reason, @Ref, datetime('now'))
                    """,
                    new
                    {
                        ItemId = write.ItemId,
                        Delta = delta,
                        Reason = adjustmentReason,
                        Ref = "StockAdmin",
                    },
                    tx,
                    cancellationToken: cancellationToken));
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task ReceiveStockAsync(
        StockPurchaseWrite purchase,
        int newStockQty,
        double costEach,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        conn.Execute(
            new CommandDefinition(
                """
                UPDATE Items
                SET StockQty = @q,
                    CostPrice = @cost,
                    UpdatedAt = datetime('now')
                WHERE Id = @id
                """,
                new { q = newStockQty, cost = costEach, id = purchase.ItemId },
                tx,
                cancellationToken: cancellationToken));

        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO StockMovements (ItemId, DeltaQty, Reason, Reference, CreatedAt)
                VALUES (@ItemId, @Delta, 'Stock received', 'ReceiveStock', datetime('now'))
                """,
                new { ItemId = purchase.ItemId, Delta = purchase.TotalItems },
                tx,
                cancellationToken: cancellationToken));

        InsertStockPurchaseCore(conn, tx, purchase, cancellationToken);

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task InsertStockPurchaseAsync(StockPurchaseWrite purchase, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        InsertStockPurchaseCore(conn, tx, purchase, cancellationToken);
        tx.Commit();
        return Task.CompletedTask;
    }

    private static void InsertStockPurchaseCore(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        StockPurchaseWrite purchase,
        CancellationToken cancellationToken)
    {
        conn.Execute(
            new CommandDefinition(
                """
                INSERT INTO StockPurchases (
                  ItemId, PurchaseDate, PacksBought, ItemsPerPack, TotalItems, TotalPaid, CostEach, Notes, CreatedAt)
                VALUES (
                  @ItemId, datetime('now'), @Packs, @PerPack, @Total, @Paid, @Each, @Notes, datetime('now'))
                """,
                new
                {
                    purchase.ItemId,
                    Packs = purchase.PacksBought,
                    PerPack = purchase.ItemsPerPack,
                    Total = purchase.TotalItems,
                    Paid = (double)purchase.TotalPaid,
                    Each = (double)purchase.CostEach,
                    Notes = string.IsNullOrWhiteSpace(purchase.Notes) ? null : purchase.Notes.Trim(),
                },
                tx,
                cancellationToken: cancellationToken));
    }

    public Task ApplyStockCountAsync(
        IReadOnlyList<(long ItemId, int NewQty)> counts,
        IReadOnlyList<long>? countedItemIds = null,
        CancellationToken cancellationToken = default)
    {
        var stampIds = countedItemIds is { Count: > 0 }
            ? countedItemIds
            : counts.Select(c => c.ItemId).Distinct().ToList();
        if (counts.Count == 0 && stampIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();

        foreach (var id in stampIds)
        {
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Items
                    SET LastStockCountDate = @dt,
                        UpdatedAt = datetime('now')
                    WHERE Id = @id
                    """,
                    new { dt = now, id },
                    tx,
                    cancellationToken: cancellationToken));
        }

        foreach (var (itemId, newQty) in counts)
        {
            var oldQty = conn.QuerySingleOrDefault<int?>(
                new CommandDefinition(
                    "SELECT StockQty FROM Items WHERE Id = @id",
                    new { id = itemId },
                    tx,
                    cancellationToken: cancellationToken));
            if (oldQty is null)
            {
                continue;
            }

            var delta = newQty - oldQty.Value;
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Items
                    SET StockQty = @q,
                        LastStockCountDate = @dt,
                        UpdatedAt = datetime('now')
                    WHERE Id = @id
                    """,
                    new { q = newQty, dt = now, id = itemId },
                    tx,
                    cancellationToken: cancellationToken));

            if (delta != 0)
            {
                conn.Execute(
                    new CommandDefinition(
                        """
                        INSERT INTO StockMovements (ItemId, DeltaQty, Reason, Reference, CreatedAt)
                        VALUES (@ItemId, @Delta, 'Stock count', 'StockCount', datetime('now'))
                        """,
                        new { ItemId = itemId, Delta = delta },
                        tx,
                        cancellationToken: cancellationToken));
            }
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task<string?> GetLatestStockCountDateAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var dt = conn.QuerySingleOrDefault<string?>(
            new CommandDefinition(
                """
                SELECT LastStockCountDate
                FROM Items
                WHERE LastStockCountDate IS NOT NULL AND trim(LastStockCountDate) != ''
                ORDER BY datetime(LastStockCountDate) DESC
                LIMIT 1
                """,
                cancellationToken: cancellationToken));
        return Task.FromResult(dt);
    }
}
