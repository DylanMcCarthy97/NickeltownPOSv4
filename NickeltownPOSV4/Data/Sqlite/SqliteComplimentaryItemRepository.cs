using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteComplimentaryItemRepository : IComplimentaryItemRepository
{
    public const string StockReasonIssue = "ComplimentaryItem";
    public const string StockReasonReversal = "ComplimentaryItemReversal";

    private readonly SqliteConnectionFactory _factory;
    private readonly IAuditLogService? _audit;

    public SqliteComplimentaryItemRepository(SqliteConnectionFactory factory, IAuditLogService? audit = null)
    {
        _factory = factory;
        _audit = audit;
    }

    public Task<ComplimentaryRecordResult> RecordAsync(
        ComplimentaryRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ItemId <= 0)
        {
            return Task.FromResult(ComplimentaryRecordResult.Fail("A product is required."));
        }

        if (request.Quantity <= 0)
        {
            return Task.FromResult(ComplimentaryRecordResult.Fail("Quantity must be at least 1."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var hasClientKey = !string.IsNullOrWhiteSpace(request.IdempotencyKey);
            var issueGuid = hasClientKey
                ? request.IdempotencyKey!.Trim()
                : Guid.NewGuid().ToString("N");

            if (hasClientKey)
            {
                var claim = MoneyIdempotencyStore.TryClaim(
                    conn,
                    tx,
                    issueGuid,
                    MoneyIdempotencyStore.KindComplimentaryItem,
                    cancellationToken);
                if (claim.AlreadyExists)
                {
                    var existing = LoadIssueByGuid(conn, tx, claim.ResultRef ?? issueGuid, cancellationToken);
                    var existingStock = existing is null
                        ? 0
                        : LoadStockQty(conn, tx, existing.ItemId, cancellationToken);
                    tx.Commit();
                    if (existing is null)
                    {
                        return Task.FromResult(ComplimentaryRecordResult.Success(
                            claim.ResultRef ?? issueGuid,
                            string.Empty,
                            request.Quantity,
                            existingStock,
                            alreadyRecorded: true));
                    }

                    return Task.FromResult(ComplimentaryRecordResult.Success(
                        existing.IssueGuid,
                        existing.ItemName,
                        existing.Quantity,
                        existingStock,
                        alreadyRecorded: true));
                }
            }

            var item = conn.QuerySingleOrDefault<ItemRow>(
                new CommandDefinition(
                    """
                    SELECT
                      Id,
                      COALESCE(NULLIF(TRIM(Name), ''), 'item') AS Name,
                      COALESCE(StockQty, 0) AS StockQty,
                      COALESCE(TrackStock, 1) AS TrackStock,
                      COALESCE(OrderInMerchandise, 0) AS OrderInMerchandise,
                      COALESCE(IsActive, 1) AS IsActive
                    FROM Items
                    WHERE Id = @id
                    LIMIT 1
                    """,
                    new { id = request.ItemId },
                    tx,
                    cancellationToken: cancellationToken));

            if (item is null)
            {
                tx.Rollback();
                return Task.FromResult(ComplimentaryRecordResult.Fail("That product was not found."));
            }

            if (item.IsActive == 0)
            {
                tx.Rollback();
                return Task.FromResult(ComplimentaryRecordResult.Fail($"“{item.Name}” is inactive."));
            }

            var skipStock = item.OrderInMerchandise != 0 || item.TrackStock == 0;
            if (!skipStock && item.StockQty < request.Quantity)
            {
                tx.Rollback();
                return Task.FromResult(ComplimentaryRecordResult.Fail(
                    item.StockQty <= 0
                        ? $"“{item.Name}” is out of stock."
                        : $"Not enough stock for “{item.Name}” (have {item.StockQty}, need {request.Quantity})."));
            }

            var retail = LoadRetailPrice(conn, tx, item.Id, cancellationToken);
            var occurred = request.OccurredAt ?? DateTimeOffset.Now;
            var occurredUtc = occurred.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            var localDate = occurred.ToLocalTime().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var staffName = string.IsNullOrWhiteSpace(request.StaffName) ? null : request.StaffName.Trim();

            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO ComplimentaryItemIssues (
                      IssueGuid, ItemId, ItemName, Quantity, UnitRetailPrice, TransactionType, Status,
                      StaffId, StaffName, OccurredAtUtc, LocalDate, IdempotencyKey, CreatedAt)
                    VALUES (
                      @IssueGuid, @ItemId, @ItemName, @Quantity, @UnitRetailPrice, @TransactionType, @Status,
                      @StaffId, @StaffName, @OccurredAtUtc, @LocalDate, @IdempotencyKey, datetime('now'))
                    """,
                    new
                    {
                        IssueGuid = issueGuid,
                        ItemId = item.Id,
                        ItemName = item.Name,
                        Quantity = request.Quantity,
                        UnitRetailPrice = retail,
                        TransactionType = ComplimentaryTransactionTypes.ComplimentaryItem,
                        Status = ComplimentaryIssueStatus.Active,
                        StaffId = request.StaffId,
                        StaffName = staffName,
                        OccurredAtUtc = occurredUtc,
                        LocalDate = localDate,
                        IdempotencyKey = hasClientKey ? issueGuid : issueGuid,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            var stockAfter = item.StockQty;
            if (!skipStock)
            {
                conn.Execute(
                    new CommandDefinition(
                        "UPDATE Items SET StockQty = StockQty - @q, UpdatedAt = datetime('now') WHERE Id = @id",
                        new { q = request.Quantity, id = item.Id },
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
                            ItemId = item.Id,
                            Delta = -request.Quantity,
                            Reason = StockReasonIssue,
                            Ref = issueGuid,
                        },
                        tx,
                        cancellationToken: cancellationToken));

                stockAfter = item.StockQty - request.Quantity;
            }

            if (hasClientKey)
            {
                MoneyIdempotencyStore.SetResultRef(conn, tx, issueGuid, issueGuid, cancellationToken);
            }

            tx.Commit();

            _ = _audit?.LogAsync(
                new AuditLogEntryRequest
                {
                    ActionType = AuditActions.ComplimentaryItemRecorded,
                    EntityType = AuditEntityTypes.ComplimentaryItem,
                    EntityId = issueGuid,
                    Amount = 0m,
                    Reason = ActivityLogText.ComplimentaryItemRecorded(item.Name, request.Quantity),
                    Success = true,
                    DetailsJson = JsonSerializer.Serialize(new
                    {
                        product = item.Name,
                        quantity = request.Quantity,
                        issueGuid,
                        retailPrice = retail,
                        staffName,
                    }),
                },
                CancellationToken.None);

            return Task.FromResult(ComplimentaryRecordResult.Success(issueGuid, item.Name, request.Quantity, stockAfter));
        }
        catch (Exception ex) when (SqliteConstraint.IsUniqueViolation(ex) && !string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Task.FromResult(ComplimentaryRecordResult.Success(
                request.IdempotencyKey!.Trim(),
                string.Empty,
                request.Quantity,
                0,
                alreadyRecorded: true));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ComplimentaryRecordResult.Fail(ex.Message));
        }
    }

    public Task<ComplimentaryReverseResult> ReverseAsync(
        string issueGuid,
        long? staffId,
        string? staffName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(issueGuid))
        {
            return Task.FromResult(ComplimentaryReverseResult.Fail("Nothing to undo."));
        }

        var guid = issueGuid.Trim();
        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var issue = LoadIssueByGuid(conn, tx, guid, cancellationToken);
            if (issue is null)
            {
                tx.Rollback();
                return Task.FromResult(ComplimentaryReverseResult.Fail("That free item was not found."));
            }

            if (string.Equals(issue.Status, ComplimentaryIssueStatus.Reversed, StringComparison.OrdinalIgnoreCase))
            {
                tx.Commit();
                return Task.FromResult(ComplimentaryReverseResult.Success(issue.ItemName, issue.Quantity, alreadyReversed: true));
            }

            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var reversedRows = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE ComplimentaryItemIssues
                    SET Status = @reversed,
                        ReversedAtUtc = @stamp,
                        ReversedByStaffId = @staffId,
                        ReversedByStaffName = @staffName
                    WHERE IssueGuid = @guid AND Status = @active
                    """,
                    new
                    {
                        reversed = ComplimentaryIssueStatus.Reversed,
                        stamp,
                        staffId,
                        staffName = string.IsNullOrWhiteSpace(staffName) ? null : staffName.Trim(),
                        guid,
                        active = ComplimentaryIssueStatus.Active,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            if (reversedRows == 0)
            {
                tx.Commit();
                return Task.FromResult(ComplimentaryReverseResult.Success(issue.ItemName, issue.Quantity, alreadyReversed: true));
            }

            var item = conn.QuerySingleOrDefault<ItemRow>(
                new CommandDefinition(
                    """
                    SELECT
                      Id,
                      COALESCE(NULLIF(TRIM(Name), ''), 'item') AS Name,
                      COALESCE(StockQty, 0) AS StockQty,
                      COALESCE(TrackStock, 1) AS TrackStock,
                      COALESCE(OrderInMerchandise, 0) AS OrderInMerchandise,
                      COALESCE(IsActive, 1) AS IsActive
                    FROM Items
                    WHERE Id = @id
                    LIMIT 1
                    """,
                    new { id = issue.ItemId },
                    tx,
                    cancellationToken: cancellationToken));

            if (item is not null)
            {
                var skipStock = item.OrderInMerchandise != 0 || item.TrackStock == 0;
                if (!skipStock)
                {
                    conn.Execute(
                        new CommandDefinition(
                            "UPDATE Items SET StockQty = StockQty + @q, UpdatedAt = datetime('now') WHERE Id = @id",
                            new { q = issue.Quantity, id = item.Id },
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
                                ItemId = item.Id,
                                Delta = issue.Quantity,
                                Reason = StockReasonReversal,
                                Ref = guid,
                            },
                            tx,
                            cancellationToken: cancellationToken));
                }
            }

            tx.Commit();

            SqliteActivityAudit.TryLog(
                _audit,
                AuditActions.ComplimentaryItemUndone,
                AuditEntityTypes.ComplimentaryItem,
                guid,
                amount: 0m,
                ActivityLogText.ComplimentaryItemUndone(issue.ItemName, issue.Quantity));

            return Task.FromResult(ComplimentaryReverseResult.Success(issue.ItemName, issue.Quantity));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ComplimentaryReverseResult.Fail(ex.Message));
        }
    }

    public Task<IReadOnlyList<QuickFreeButtonRow>> GetButtonsAsync(
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var date = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var rows = conn.Query<QuickFreeButtonRow>(
            new CommandDefinition(
                $"""
                SELECT
                  c.ItemId AS ItemId,
                  COALESCE(NULLIF(TRIM(i.Name), ''), 'item') AS ProductName,
                  COALESCE(NULLIF(TRIM(c.DisplayLabel), ''), NULLIF(TRIM(i.Name), ''), 'ITEM') AS DisplayLabel,
                  c.Icon AS Icon,
                  c.SortOrder AS SortOrder,
                  COALESCE(i.StockQty, 0) AS StockQty,
                  CAST(CASE WHEN COALESCE(i.TrackStock, 1) != 0 AND COALESCE(i.OrderInMerchandise, 0) = 0 THEN 1 ELSE 0 END AS INTEGER) AS TrackStock,
                  CAST(COALESCE((
                    SELECT SUM(iss.Quantity)
                    FROM ComplimentaryItemIssues iss
                    WHERE iss.ItemId = c.ItemId
                      AND iss.LocalDate = @date
                      AND iss.Status = '{ComplimentaryIssueStatus.Active}'
                  ), 0) AS INTEGER) AS TodayCount,
                  CAST(({RetailPriceSql("c.ItemId")}) AS REAL) AS UnitRetailPrice
                FROM QuickFreeItemConfig c
                INNER JOIN Items i ON i.Id = c.ItemId
                WHERE COALESCE(i.IsActive, 1) != 0
                ORDER BY c.SortOrder, i.Name COLLATE NOCASE
                """,
                new { date },
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<QuickFreeButtonRow>>(rows.AsList());
    }

    public Task<int> GetTodayCountAsync(
        long itemId,
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var count = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                SELECT COALESCE(SUM(Quantity), 0)
                FROM ComplimentaryItemIssues
                WHERE ItemId = @itemId
                  AND LocalDate = @date
                  AND Status = @status
                """,
                new
                {
                    itemId,
                    date = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    status = ComplimentaryIssueStatus.Active,
                },
                cancellationToken: cancellationToken));
        return Task.FromResult((int)count);
    }

    public Task<ComplimentaryReport> GetReportAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        if (toInclusive < fromInclusive)
        {
            (fromInclusive, toInclusive) = (toInclusive, fromInclusive);
        }

        using var conn = _factory.OpenConnection();
        var lines = conn.Query<ComplimentaryReportLine>(
            new CommandDefinition(
                """
                SELECT
                  ItemId AS ItemId,
                  COALESCE(NULLIF(TRIM(MAX(ItemName)), ''), 'item') AS ItemName,
                  CAST(SUM(Quantity) AS INTEGER) AS Quantity,
                  CAST(CASE WHEN SUM(Quantity) > 0 THEN SUM(Quantity * UnitRetailPrice) / SUM(Quantity) ELSE 0 END AS REAL) AS UnitRetailPrice,
                  CAST(SUM(Quantity * UnitRetailPrice) AS REAL) AS RetailValue
                FROM ComplimentaryItemIssues
                WHERE LocalDate >= @from
                  AND LocalDate <= @to
                  AND Status = @status
                GROUP BY ItemId
                ORDER BY ItemName COLLATE NOCASE
                """,
                new
                {
                    from = fromInclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    to = toInclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    status = ComplimentaryIssueStatus.Active,
                },
                cancellationToken: cancellationToken)).AsList();

        var totalItems = lines.Sum(l => l.Quantity);
        var totalRetail = decimal.Round(lines.Sum(l => l.RetailValue), 2, MidpointRounding.AwayFromZero);
        return Task.FromResult(new ComplimentaryReport
        {
            From = fromInclusive,
            To = toInclusive,
            Lines = lines,
            TotalItems = totalItems,
            TotalRetailValue = totalRetail,
        });
    }

    public Task<IReadOnlyList<QuickFreeConfigRow>> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<QuickFreeConfigRow>(
            new CommandDefinition(
                """
                SELECT
                  c.Id AS Id,
                  c.ItemId AS ItemId,
                  COALESCE(NULLIF(TRIM(i.Name), ''), 'item') AS ProductName,
                  c.DisplayLabel AS DisplayLabel,
                  c.Icon AS Icon,
                  c.SortOrder AS SortOrder,
                  CAST(CASE WHEN COALESCE(i.IsActive, 1) != 0 THEN 1 ELSE 0 END AS INTEGER) AS ProductIsActive
                FROM QuickFreeItemConfig c
                INNER JOIN Items i ON i.Id = c.ItemId
                ORDER BY c.SortOrder, i.Name COLLATE NOCASE
                """,
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<QuickFreeConfigRow>>(rows.AsList());
    }

    public Task<IReadOnlyList<QuickFreeProductCandidate>> GetProductCandidatesAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<QuickFreeProductCandidate>(
            new CommandDefinition(
                $"""
                SELECT
                  i.Id AS ItemId,
                  COALESCE(NULLIF(TRIM(i.Name), ''), 'item') AS Name,
                  CAST(({RetailPriceSql("i.Id")}) AS REAL) AS PitstopPrice,
                  COALESCE(i.StockQty, 0) AS StockQty
                FROM Items i
                WHERE COALESCE(i.IsActive, 1) != 0
                  AND NOT EXISTS (SELECT 1 FROM QuickFreeItemConfig c WHERE c.ItemId = i.Id)
                ORDER BY i.Name COLLATE NOCASE
                """,
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<QuickFreeProductCandidate>>(rows.AsList());
    }

    public Task<ComplimentaryConfigResult> AddConfigAsync(
        long itemId,
        string? displayLabel,
        string? icon,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
        {
            return Task.FromResult(ComplimentaryConfigResult.Fail("Select a product."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var name = conn.ExecuteScalar<string?>(
                new CommandDefinition(
                    "SELECT Name FROM Items WHERE Id = @id AND COALESCE(IsActive, 1) != 0 LIMIT 1",
                    new { id = itemId },
                    tx,
                    cancellationToken: cancellationToken));
            if (string.IsNullOrWhiteSpace(name))
            {
                tx.Rollback();
                return Task.FromResult(ComplimentaryConfigResult.Fail("That product was not found or is inactive."));
            }

            var exists = conn.ExecuteScalar<long>(
                new CommandDefinition(
                    "SELECT COUNT(1) FROM QuickFreeItemConfig WHERE ItemId = @id",
                    new { id = itemId },
                    tx,
                    cancellationToken: cancellationToken));
            if (exists > 0)
            {
                tx.Rollback();
                return Task.FromResult(ComplimentaryConfigResult.Fail("That product is already a Quick Free Item."));
            }

            var nextOrder = (int)conn.ExecuteScalar<long>(
                new CommandDefinition(
                    "SELECT COALESCE(MAX(SortOrder), 0) + 1 FROM QuickFreeItemConfig",
                    transaction: tx,
                    cancellationToken: cancellationToken));

            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO QuickFreeItemConfig (ItemId, DisplayLabel, Icon, SortOrder, CreatedAt, UpdatedAt)
                    VALUES (@ItemId, @DisplayLabel, @Icon, @SortOrder, datetime('now'), datetime('now'))
                    """,
                    new
                    {
                        ItemId = itemId,
                        DisplayLabel = NullIfBlank(displayLabel),
                        Icon = NullIfBlank(icon),
                        SortOrder = nextOrder,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            tx.Commit();

            SqliteActivityAudit.TryLog(
                _audit,
                AuditActions.QuickFreeItemAdded,
                AuditEntityTypes.QuickFreeItemConfig,
                itemId.ToString(CultureInfo.InvariantCulture),
                amount: null,
                ActivityLogText.QuickFreeItemAdded(name));

            return Task.FromResult(ComplimentaryConfigResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ComplimentaryConfigResult.Fail(ex.Message));
        }
    }

    public Task<ComplimentaryConfigResult> RemoveConfigAsync(
        long itemId,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
        {
            return Task.FromResult(ComplimentaryConfigResult.Fail("Select a product."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            var name = conn.ExecuteScalar<string?>(
                "SELECT COALESCE(NULLIF(TRIM(i.Name), ''), 'item') FROM QuickFreeItemConfig c INNER JOIN Items i ON i.Id = c.ItemId WHERE c.ItemId = @id LIMIT 1",
                new { id = itemId });
            var removed = conn.Execute(
                "DELETE FROM QuickFreeItemConfig WHERE ItemId = @id",
                new { id = itemId });
            if (removed == 0)
            {
                return Task.FromResult(ComplimentaryConfigResult.Fail("That product is not in Quick Free Items."));
            }

            SqliteActivityAudit.TryLog(
                _audit,
                AuditActions.QuickFreeItemRemoved,
                AuditEntityTypes.QuickFreeItemConfig,
                itemId.ToString(CultureInfo.InvariantCulture),
                amount: null,
                ActivityLogText.QuickFreeItemRemoved(name ?? "item"));

            return Task.FromResult(ComplimentaryConfigResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ComplimentaryConfigResult.Fail(ex.Message));
        }
    }

    public Task<ComplimentaryConfigResult> UpdateConfigAsync(
        long itemId,
        string? displayLabel,
        string? icon,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
        {
            return Task.FromResult(ComplimentaryConfigResult.Fail("Select a product."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            var updated = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE QuickFreeItemConfig
                    SET DisplayLabel = @DisplayLabel,
                        Icon = @Icon,
                        UpdatedAt = datetime('now')
                    WHERE ItemId = @ItemId
                    """,
                    new
                    {
                        ItemId = itemId,
                        DisplayLabel = NullIfBlank(displayLabel),
                        Icon = NullIfBlank(icon),
                    },
                    cancellationToken: cancellationToken));
            if (updated == 0)
            {
                return Task.FromResult(ComplimentaryConfigResult.Fail("That product is not in Quick Free Items."));
            }

            var name = conn.ExecuteScalar<string?>(
                "SELECT COALESCE(NULLIF(TRIM(Name), ''), 'item') FROM Items WHERE Id = @id LIMIT 1",
                new { id = itemId });
            SqliteActivityAudit.TryLog(
                _audit,
                AuditActions.QuickFreeItemConfigChanged,
                AuditEntityTypes.QuickFreeItemConfig,
                itemId.ToString(CultureInfo.InvariantCulture),
                amount: null,
                ActivityLogText.QuickFreeItemConfigChanged(name ?? "item"));

            return Task.FromResult(ComplimentaryConfigResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ComplimentaryConfigResult.Fail(ex.Message));
        }
    }

    public Task<ComplimentaryConfigResult> MoveConfigAsync(
        long itemId,
        int direction,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0 || direction == 0)
        {
            return Task.FromResult(ComplimentaryConfigResult.Fail("Nothing to move."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();
            var rows = conn.Query<(long ItemId, int SortOrder)>(
                new CommandDefinition(
                    "SELECT ItemId, SortOrder FROM QuickFreeItemConfig ORDER BY SortOrder, ItemId",
                    transaction: tx,
                    cancellationToken: cancellationToken)).AsList();
            var index = rows.FindIndex(r => r.ItemId == itemId);
            if (index < 0)
            {
                tx.Rollback();
                return Task.FromResult(ComplimentaryConfigResult.Fail("That product is not in Quick Free Items."));
            }

            var swapWith = index + (direction < 0 ? -1 : 1);
            if (swapWith < 0 || swapWith >= rows.Count)
            {
                tx.Commit();
                return Task.FromResult(ComplimentaryConfigResult.Success());
            }

            var a = rows[index];
            var b = rows[swapWith];
            conn.Execute(
                new CommandDefinition(
                    "UPDATE QuickFreeItemConfig SET SortOrder = @order, UpdatedAt = datetime('now') WHERE ItemId = @id",
                    new { order = b.SortOrder, id = a.ItemId },
                    tx,
                    cancellationToken: cancellationToken));
            conn.Execute(
                new CommandDefinition(
                    "UPDATE QuickFreeItemConfig SET SortOrder = @order, UpdatedAt = datetime('now') WHERE ItemId = @id",
                    new { order = a.SortOrder, id = b.ItemId },
                    tx,
                    cancellationToken: cancellationToken));
            tx.Commit();
            return Task.FromResult(ComplimentaryConfigResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ComplimentaryConfigResult.Fail(ex.Message));
        }
    }

    private static string RetailPriceSql(string itemIdExpr) =>
        $"""
        COALESCE(
          (
            SELECT ipp.Price
            FROM ItemPrices ipp
            WHERE ipp.ItemId = {itemIdExpr}
              AND lower(trim(COALESCE(ipp.PriceKind, ''))) = 'pitstop'
            ORDER BY datetime(ipp.EffectiveFrom) DESC, ipp.Id DESC
            LIMIT 1
          ),
          (
            SELECT ipb.Price
            FROM ItemPrices ipb
            WHERE ipb.ItemId = {itemIdExpr}
              AND COALESCE(ipb.PriceKind, 'Bar') = 'Bar'
            ORDER BY datetime(ipb.EffectiveFrom) DESC, ipb.Id DESC
            LIMIT 1
          ),
          0)
        """;

    private static decimal LoadRetailPrice(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        Microsoft.Data.Sqlite.SqliteTransaction tx,
        long itemId,
        CancellationToken cancellationToken)
    {
        var value = conn.ExecuteScalar<double?>(
            new CommandDefinition(
                $"SELECT {RetailPriceSql("@id")}",
                new { id = itemId },
                tx,
                cancellationToken: cancellationToken));
        return decimal.Round((decimal)(value ?? 0d), 2, MidpointRounding.AwayFromZero);
    }

    private static int LoadStockQty(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        Microsoft.Data.Sqlite.SqliteTransaction tx,
        long itemId,
        CancellationToken cancellationToken)
    {
        return conn.ExecuteScalar<int>(
            new CommandDefinition(
                "SELECT COALESCE(StockQty, 0) FROM Items WHERE Id = @id LIMIT 1",
                new { id = itemId },
                tx,
                cancellationToken: cancellationToken));
    }

    private static IssueRow? LoadIssueByGuid(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        Microsoft.Data.Sqlite.SqliteTransaction tx,
        string guid,
        CancellationToken cancellationToken)
    {
        return conn.QuerySingleOrDefault<IssueRow>(
            new CommandDefinition(
                """
                SELECT IssueGuid, ItemId, ItemName, Quantity, Status
                FROM ComplimentaryItemIssues
                WHERE IssueGuid = @guid
                LIMIT 1
                """,
                new { guid },
                tx,
                cancellationToken: cancellationToken));
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ItemRow
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int StockQty { get; set; }

        public int TrackStock { get; set; }

        public int OrderInMerchandise { get; set; }

        public int IsActive { get; set; }
    }

    private sealed class IssueRow
    {
        public string IssueGuid { get; set; } = string.Empty;

        public long ItemId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}
