using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DateTimeStyles = System.Globalization.DateTimeStyles;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqlitePitstopRetailSaleRepository : IPitstopRetailSaleRepository
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ISquarePaymentAttemptRepository _paymentAttempts;

    public SqlitePitstopRetailSaleRepository(
        SqliteConnectionFactory factory,
        ISquarePaymentAttemptRepository paymentAttempts)
    {
        _factory = factory;
        _paymentAttempts = paymentAttempts;
    }

    public async Task<PitstopSaleCommitResult> CommitSaleAsync(
        IReadOnlyList<PitstopSaleLineCommit> lines,
        PitstopSalePaymentCommit payment,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            return PitstopSaleCommitResult.Fail("Cart is empty.");
        }

        var method = (payment.PaymentMethod ?? string.Empty).Trim();
        if (method.Length == 0)
        {
            return PitstopSaleCommitResult.Fail("Payment method is required.");
        }

        foreach (var line in lines)
        {
            if (line.Quantity <= 0)
            {
                return PitstopSaleCommitResult.Fail("Each line needs a positive quantity.");
            }
        }

        var baseProduct = decimal.Round(payment.BaseProductTotal, 2, MidpointRounding.AwayFromZero);
        var fee = payment.CardSurchargeAmount.HasValue
            ? decimal.Round(payment.CardSurchargeAmount.Value, 2, MidpointRounding.AwayFromZero)
            : 0m;
        if (fee < 0m)
        {
            return PitstopSaleCommitResult.Fail("Card surcharge cannot be negative.");
        }

        var charged = decimal.Round(payment.ChargedTotal, 2, MidpointRounding.AwayFromZero);
        if (charged <= 0m)
        {
            return PitstopSaleCommitResult.Fail("Sale total must be positive.");
        }

        var computedBase = decimal.Round(lines.Sum(l => l.UnitPrice * l.Quantity), 2, MidpointRounding.AwayFromZero);
        if (computedBase != baseProduct)
        {
            return PitstopSaleCommitResult.Fail("Line totals do not match recorded base total.");
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(payment.IdempotencyKey)
            ? null
            : payment.IdempotencyKey.Trim();

        long savedSalePk;
        string savedSaleGuid;
        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                var existingGuid = conn.ExecuteScalar<string?>(
                    new CommandDefinition(
                        "SELECT SaleGuid FROM PitstopSales WHERE IdempotencyKey = @key LIMIT 1",
                        new { key = idempotencyKey },
                        tx,
                        cancellationToken: cancellationToken));

                if (!string.IsNullOrEmpty(existingGuid))
                {
                    var existingPk = conn.ExecuteScalar<long?>(
                        new CommandDefinition(
                            "SELECT Id FROM PitstopSales WHERE SaleGuid = @g LIMIT 1",
                            new { g = existingGuid },
                            tx,
                            cancellationToken: cancellationToken));
                    tx.Commit();
                    return PitstopSaleCommitResult.Success(existingPk ?? 0, existingGuid);
                }
            }

            if (!string.IsNullOrWhiteSpace(payment.SquareExternalRef))
            {
                var dupSquare = conn.ExecuteScalar<long>(
                    new CommandDefinition(
                        """
                        SELECT COUNT(1) FROM PitstopSales
                        WHERE SquareExternalRef = @squareRef AND trim(COALESCE(SquareExternalRef,'')) != ''
                        """,
                        new { squareRef = payment.SquareExternalRef.Trim() },
                        tx,
                        cancellationToken: cancellationToken));

                if (dupSquare > 0)
                {
                    tx.Rollback();
                    return PitstopSaleCommitResult.Fail("This card sale was already recorded.");
                }
            }

            var saleGuid = "v4ps_" + Guid.NewGuid().ToString("N");
            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var headerRaw = JsonSerializer.Serialize(new
            {
                saleGuid,
                paymentMethod = method,
                payment.BartenderId,
                payment.StaffDisplayName,
                payment.SquareExternalRef,
                baseProductTotal = baseProduct,
                cardSurchargePercent = payment.CardSurchargePercent,
                cardSurchargeAmount = fee > 0m ? fee : (decimal?)null,
                chargedTotal = charged,
                status = "Completed",
            });

            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO PitstopSales (
                      LegacyId, SaleGuid, SoldAt, Total, PaymentMethod, SaleMode,
                      BartenderId, StaffDisplayName, SquareExternalRef, SquareCheckoutId,
                      BaseProductTotal, CardSurchargePercent, CardSurchargeAmount,
                      IdempotencyKey, CashReceived, CashChange,
                      Status, StockWasDeducted,
                      RawJson, CreatedAt)
                    VALUES (
                      @LegacyId, @SaleGuid, @SoldAt, @Total, @PaymentMethod, @SaleMode,
                      @BartenderId, @StaffDisplayName, @SquareExternalRef, @SquareCheckoutId,
                      @BaseProductTotal, @CardSurchargePercent, @CardSurchargeAmount,
                      @IdempotencyKey, @CashReceived, @CashChange,
                      'Active', 1,
                      @RawJson, datetime('now'))
                    """,
                    new
                    {
                        LegacyId = saleGuid,
                        SaleGuid = saleGuid,
                        SoldAt = stamp,
                        Total = charged,
                        PaymentMethod = method,
                        SaleMode = "Pitstop",
                        BartenderId = payment.BartenderId,
                        StaffDisplayName = string.IsNullOrWhiteSpace(payment.StaffDisplayName) ? null : payment.StaffDisplayName.Trim(),
                        SquareExternalRef = string.IsNullOrWhiteSpace(payment.SquareExternalRef) ? null : payment.SquareExternalRef.Trim(),
                        SquareCheckoutId = string.IsNullOrWhiteSpace(payment.SquareCheckoutId) ? null : payment.SquareCheckoutId.Trim(),
                        BaseProductTotal = baseProduct,
                        CardSurchargePercent = payment.CardSurchargePercent,
                        CardSurchargeAmount = fee > 0m ? fee : (decimal?)null,
                        IdempotencyKey = idempotencyKey,
                        CashReceived = payment.CashReceived,
                        CashChange = payment.CashChange,
                        RawJson = headerRaw,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            var salePk = conn.QuerySingle<long>(
                new CommandDefinition(
                    "SELECT Id FROM PitstopSales WHERE SaleGuid = @g OR LegacyId = @g LIMIT 1",
                    new { g = saleGuid },
                    tx,
                    cancellationToken: cancellationToken));

            var anyLineDeductedStock = false;
            foreach (var line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = conn.QuerySingleOrDefault<ItemStockRow>(
                    new CommandDefinition(
                        """
                        SELECT StockQty, COALESCE(TrackStock, 1) AS TrackStock, COALESCE(OrderInMerchandise, 0) AS OrderInMerchandise
                        FROM Items
                        WHERE Id = @id AND COALESCE(IsActive,1) != 0
                        """,
                        new { id = line.ItemId },
                        tx,
                        cancellationToken: cancellationToken));

                if (row is null)
                {
                    tx.Rollback();
                    return PitstopSaleCommitResult.Fail($"Item id {line.ItemId} is missing or inactive.");
                }

                var skipStockForSale = row.OrderInMerchandise != 0;
                if (!skipStockForSale && row.TrackStock != 0 && row.StockQty < line.Quantity)
                {
                    tx.Rollback();
                    return PitstopSaleCommitResult.Fail($"Not enough stock for \"{line.DisplayName}\".");
                }

                var lineTotal = decimal.Round(line.UnitPrice * line.Quantity, 2, MidpointRounding.AwayFromZero);
                var legacyLineId = saleGuid + ":l" + line.ItemId;
                var lineRaw = JsonSerializer.Serialize(new
                {
                    line.ItemId,
                    line.DisplayName,
                    line.Sku,
                    line.UnitPrice,
                    line.Quantity,
                    lineTotal,
                    categoryName = line.CategoryName,
                    subCategory = line.SubCategory,
                });

                conn.Execute(
                    new CommandDefinition(
                        """
                        INSERT INTO PitstopSaleItems (PitstopSaleId, LegacyLineId, Sku, ItemName, ItemId, Quantity, UnitPrice, LineTotal, RawJson, CreatedAt)
                        VALUES (@PitstopSaleId, @LegacyLineId, @Sku, @ItemName, @ItemId, @Quantity, @UnitPrice, @LineTotal, @RawJson, datetime('now'))
                        """,
                        new
                        {
                            PitstopSaleId = salePk,
                            LegacyLineId = legacyLineId,
                            Sku = line.Sku,
                            ItemName = line.DisplayName,
                            ItemId = line.ItemId,
                            Quantity = line.Quantity,
                            UnitPrice = line.UnitPrice,
                            LineTotal = lineTotal,
                            RawJson = lineRaw,
                        },
                        tx,
                        cancellationToken: cancellationToken));

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
                                Reason = "PitstopRetailSale",
                                Ref = saleGuid,
                            },
                            tx,
                            cancellationToken: cancellationToken));

                    anyLineDeductedStock = true;
                }
            }

            if (!anyLineDeductedStock)
            {
                conn.Execute(
                    new CommandDefinition(
                        "UPDATE PitstopSales SET StockWasDeducted = 0 WHERE Id = @id",
                        new { id = salePk },
                        tx,
                        cancellationToken: cancellationToken));
            }

            if (fee > 0m)
            {
                var feeLegacyId = saleGuid + ":fee:card_surcharge";
                var feeRaw = JsonSerializer.Serialize(new
                {
                    kind = "CardSurcharge",
                    displayName = "Card processing (Square) pass-through",
                    amount = fee,
                    percent = payment.CardSurchargePercent,
                });

                conn.Execute(
                    new CommandDefinition(
                        """
                        INSERT INTO PitstopSaleItems (PitstopSaleId, LegacyLineId, Sku, ItemName, ItemId, Quantity, UnitPrice, LineTotal, RawJson, CreatedAt)
                        VALUES (@PitstopSaleId, @LegacyLineId, NULL, @ItemName, NULL, 1, @UnitPrice, @LineTotal, @RawJson, datetime('now'))
                        """,
                        new
                        {
                            PitstopSaleId = salePk,
                            LegacyLineId = feeLegacyId,
                            ItemName = "Card processing (Square)",
                            UnitPrice = fee,
                            LineTotal = fee,
                            RawJson = feeRaw,
                        },
                        tx,
                        cancellationToken: cancellationToken));
            }

            tx.Commit();
            savedSalePk = salePk;
            savedSaleGuid = saleGuid;
        }
        catch (Exception ex)
        {
            return PitstopSaleCommitResult.Fail(ex.Message);
        }

        if (payment.PaymentAttemptId is > 0)
        {
            try
            {
                var marked = await _paymentAttempts
                    .MarkCompletedAsync(
                        payment.PaymentAttemptId.Value,
                        payment.SquareExternalRef ?? idempotencyKey ?? savedSaleGuid,
                        null,
                        savedSalePk,
                        savedSaleGuid,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!marked)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PitstopRetail] MarkCompletedAsync returned false for attempt {payment.PaymentAttemptId.Value} (sale {savedSaleGuid}). Square Recovery should surface this.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PitstopRetail] MarkCompletedAsync failed for attempt {payment.PaymentAttemptId.Value}: {ex.Message}");
            }
        }

        return PitstopSaleCommitResult.Success(savedSalePk, savedSaleGuid);
    }

    public Task<IReadOnlyList<PitstopSaleLineReportRow>> GetItemisedLinesAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var startIso = startInclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endIso = endExclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var rows = conn.Query<PitstopSaleLineReportRow>(
            new CommandDefinition(
                """
                SELECT
                  COALESCE(li.ItemId, 0) AS ItemId,
                  COALESCE(NULLIF(TRIM(li.ItemName), ''), '(Unknown)') AS ItemName,
                  COALESCE(NULLIF(TRIM(c.Name), ''), 'General') AS CategoryName,
                  COALESCE(NULLIF(TRIM(ps.PaymentMethod), ''), '—') AS PaymentMethod,
                  li.Quantity AS Quantity,
                  COALESCE(li.UnitPrice, CASE WHEN li.Quantity > 0 THEN li.LineTotal / li.Quantity ELSE 0 END) AS UnitPrice,
                  COALESCE(li.LineTotal, 0) AS LineTotal
                FROM PitstopSaleItems li
                INNER JOIN PitstopSales ps ON ps.Id = li.PitstopSaleId
                LEFT JOIN Items i ON i.Id = li.ItemId
                LEFT JOIN Categories c ON c.Id = i.CategoryId
                WHERE lower(trim(COALESCE(ps.SaleMode, ''))) = 'pitstop'
                  AND ps.PitstopEodBatchId IS NULL
                  AND COALESCE(ps.Status,'Active') = 'Active'
                  AND datetime(COALESCE(ps.SoldAt, ps.CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(ps.SoldAt, ps.CreatedAt)) < datetime(@endIso)
                ORDER BY datetime(COALESCE(ps.SoldAt, ps.CreatedAt)), li.Id
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken));
        return Task.FromResult<IReadOnlyList<PitstopSaleLineReportRow>>(rows.AsList());
    }

    public Task<PitstopRetailPeriodTotals> GetPitstopRetailPaymentTotalsAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var startIso = startInclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endIso = endExclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        var row = conn.QuerySingleOrDefault<PitstopTotalsAggRow>(
            new CommandDefinition(
                """
                SELECT
                  COALESCE(SUM(CASE WHEN lower(trim(COALESCE(PaymentMethod,''))) = 'cash' THEN Total ELSE 0 END), 0) AS CashTotal,
                  COALESCE(SUM(CASE WHEN lower(trim(COALESCE(PaymentMethod,''))) IN ('card','square') THEN Total ELSE 0 END), 0) AS CardChargedTotal,
                  COALESCE(SUM(
                    CASE
                      WHEN lower(trim(COALESCE(PaymentMethod,''))) IN ('card','square') THEN
                        COALESCE(
                          BaseProductTotal,
                          CASE
                            WHEN COALESCE(CardSurchargeAmount, 0) > 0 THEN Total - CardSurchargeAmount
                            ELSE Total
                          END
                        )
                      ELSE 0
                    END
                  ), 0) AS CardBaseProductTotal,
                  COALESCE(SUM(
                    CASE
                      WHEN lower(trim(COALESCE(PaymentMethod,''))) IN ('card','square') THEN COALESCE(CardSurchargeAmount, 0)
                      ELSE 0
                    END
                  ), 0) AS CardSurchargeCollected
                FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND PitstopEodBatchId IS NULL
                  AND COALESCE(Status,'Active') = 'Active'
                  AND datetime(COALESCE(SoldAt, CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(SoldAt, CreatedAt)) < datetime(@endIso)
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return Task.FromResult(new PitstopRetailPeriodTotals());
        }

        return Task.FromResult(
            new PitstopRetailPeriodTotals
            {
                CashTotal = decimal.Round(row.CashTotal, 2, MidpointRounding.AwayFromZero),
                CardChargedTotal = decimal.Round(row.CardChargedTotal, 2, MidpointRounding.AwayFromZero),
                CardBaseProductTotal = decimal.Round(row.CardBaseProductTotal, 2, MidpointRounding.AwayFromZero),
                CardSurchargeCollected = decimal.Round(row.CardSurchargeCollected, 2, MidpointRounding.AwayFromZero),
            });
    }

    public Task<IReadOnlyList<PitstopCardSaleRefRow>> GetPitstopCardSalesForPeriodAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var startIso = startInclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endIso = endExclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        var rows = conn.Query<PitstopCardSaleDbRow>(
            new CommandDefinition(
                """
                SELECT
                  Id AS SaleId,
                  COALESCE(SaleGuid, LegacyId, cast(Id as text)) AS SaleRef,
                  Total,
                  trim(SquareExternalRef) AS SquareExternalRef,
                  COALESCE(SoldAt, CreatedAt) AS SoldAtIso
                FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND PitstopEodBatchId IS NULL
                  AND COALESCE(Status,'Active') = 'Active'
                  AND lower(trim(COALESCE(PaymentMethod,''))) IN ('card','square')
                  AND SquareExternalRef IS NOT NULL AND trim(SquareExternalRef) != ''
                  AND datetime(COALESCE(SoldAt, CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(SoldAt, CreatedAt)) < datetime(@endIso)
                ORDER BY datetime(COALESCE(SoldAt, CreatedAt)), Id
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken));

        var list = rows.Select(r => new PitstopCardSaleRefRow
        {
            SaleId = r.SaleId,
            SaleRef = r.SaleRef ?? r.SaleId.ToString(CultureInfo.InvariantCulture),
            Total = decimal.Round(r.Total, 2, MidpointRounding.AwayFromZero),
            SquareExternalRef = r.SquareExternalRef ?? string.Empty,
            SoldAt = ParseOffset(r.SoldAtIso),
        }).ToList();

        return Task.FromResult<IReadOnlyList<PitstopCardSaleRefRow>>(list);
    }

    public Task<PitstopDaySalesClearResult> ClearPitstopRetailSalesForPeriodAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        _ = startInclusive;
        _ = endExclusive;
        _ = cancellationToken;
        throw new NotSupportedException(
            "Pitstop sales must be archived after EOD export, not deleted. Use ArchiveActivePitstopSalesAsync on the Pitstop EOD batch repository.");
    }

    public Task<IReadOnlyList<PitstopActiveSaleRow>> GetActivePitstopSalesAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<PitstopActiveSaleDbRow>(
            new CommandDefinition(
                """
                SELECT
                  Id AS SaleId,
                  COALESCE(SoldAt, CreatedAt) AS SoldAtIso,
                  Total,
                  COALESCE(NULLIF(TRIM(PaymentMethod), ''), '—') AS PaymentMethod,
                  StaffDisplayName,
                  SquareExternalRef,
                  COALESCE(NULLIF(TRIM(Status), ''), 'Active') AS Status,
                  COALESCE(StockWasDeducted, 0) AS StockWasDeducted
                FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND PitstopEodBatchId IS NULL
                  AND COALESCE(Status,'Active') = 'Active'
                ORDER BY datetime(COALESCE(SoldAt, CreatedAt)) DESC, Id DESC
                """,
                cancellationToken: cancellationToken));

        var list = rows.Select(r => new PitstopActiveSaleRow
        {
            SaleId = r.SaleId,
            SoldAt = ParseOffset(r.SoldAtIso),
            Total = decimal.Round(r.Total, 2, MidpointRounding.AwayFromZero),
            PaymentMethod = r.PaymentMethod,
            StaffDisplayName = r.StaffDisplayName,
            SquareExternalRef = r.SquareExternalRef,
            Status = r.Status,
            StockWasDeducted = r.StockWasDeducted != 0,
        }).ToList();

        return Task.FromResult<IReadOnlyList<PitstopActiveSaleRow>>(list);
    }

    public Task<int> GetVoidedPitstopSaleCountForPeriodAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var startIso = startInclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endIso = endExclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        var count = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND COALESCE(Status,'Active') = 'Voided'
                  AND datetime(COALESCE(VoidedAt, SoldAt, CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(VoidedAt, SoldAt, CreatedAt)) < datetime(@endIso)
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken));

        return Task.FromResult(count);
    }

    public Task<PitstopVoidSaleResult> VoidPitstopSaleAsync(
        PitstopVoidSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SaleId <= 0)
        {
            return Task.FromResult(PitstopVoidSaleResult.Fail("Invalid sale id."));
        }

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
        {
            return Task.FromResult(PitstopVoidSaleResult.Fail("A reason is required to void a sale."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var head = conn.QuerySingleOrDefault<PitstopVoidHeadRow>(
                new CommandDefinition(
                    """
                    SELECT
                      Id AS SaleId,
                      COALESCE(NULLIF(TRIM(Status), ''), 'Active') AS Status,
                      Total,
                      COALESCE(NULLIF(TRIM(PaymentMethod), ''), '') AS PaymentMethod,
                      COALESCE(StockWasDeducted, 0) AS StockWasDeducted,
                      PitstopEodBatchId,
                      SaleGuid,
                      SquareExternalRef
                    FROM PitstopSales WHERE Id = @id LIMIT 1
                    """,
                    new { id = request.SaleId },
                    tx,
                    cancellationToken: cancellationToken));

            if (head is null)
            {
                tx.Rollback();
                return Task.FromResult(PitstopVoidSaleResult.Fail("Sale was not found."));
            }

            if (string.Equals(head.Status, "Voided", StringComparison.OrdinalIgnoreCase))
            {
                tx.Rollback();
                return Task.FromResult(PitstopVoidSaleResult.Fail("That sale is already voided."));
            }

            if (string.Equals(head.Status, "Archived", StringComparison.OrdinalIgnoreCase) || head.PitstopEodBatchId is not null)
            {
                tx.Rollback();
                return Task.FromResult(
                    PitstopVoidSaleResult.Fail(
                        "This sale is archived. Archived Pitstop sales are read-only; use Treasurer/Admin tools if a correction is required."));
            }

            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE PitstopSales
                    SET Status = 'Voided',
                        VoidedAt = @stamp,
                        VoidedByStaffId = @staffId,
                        VoidedByStaffName = @staffName,
                        VoidReason = @reason,
                        UpdatedAt = datetime('now')
                    WHERE Id = @id
                    """,
                    new
                    {
                        id = head.SaleId,
                        stamp,
                        staffId = request.StaffId,
                        staffName = string.IsNullOrWhiteSpace(request.StaffDisplayName) ? null : request.StaffDisplayName.Trim(),
                        reason,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            var stockRestored = false;
            if (head.StockWasDeducted != 0)
            {
                var lineRows = conn.Query<PitstopVoidLineRow>(
                    new CommandDefinition(
                        """
                        SELECT li.ItemId, li.Quantity
                        FROM PitstopSaleItems li
                        WHERE li.PitstopSaleId = @id AND COALESCE(li.ItemId, 0) > 0 AND COALESCE(li.Quantity, 0) > 0
                        """,
                        new { id = head.SaleId },
                        tx,
                        cancellationToken: cancellationToken)).AsList();

                foreach (var line in lineRows)
                {
                    var trackable = conn.QuerySingleOrDefault<int>(
                        new CommandDefinition(
                            """
                            SELECT
                              CASE WHEN COALESCE(TrackStock, 1) != 0
                                        AND COALESCE(OrderInMerchandise, 0) = 0
                                   THEN 1 ELSE 0 END
                            FROM Items
                            WHERE Id = @id
                            """,
                            new { id = line.ItemId },
                            tx,
                            cancellationToken: cancellationToken));

                    if (trackable == 0)
                    {
                        continue;
                    }

                    conn.Execute(
                        new CommandDefinition(
                            "UPDATE Items SET StockQty = StockQty + @q, UpdatedAt = datetime('now') WHERE Id = @id",
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
                                Delta = line.Quantity,
                                Reason = "PitstopVoidRestore",
                                Ref = head.SaleGuid ?? ("sale#" + head.SaleId.ToString(CultureInfo.InvariantCulture)),
                            },
                            tx,
                            cancellationToken: cancellationToken));

                    stockRestored = true;
                }

                if (stockRestored)
                {
                    conn.Execute(
                        new CommandDefinition(
                            "UPDATE PitstopSales SET StockWasDeducted = 0 WHERE Id = @id",
                            new { id = head.SaleId },
                            tx,
                            cancellationToken: cancellationToken));
                }
            }

            tx.Commit();

            var isSquare = !string.IsNullOrWhiteSpace(head.SquareExternalRef)
                || head.PaymentMethod.Equals("Square", StringComparison.OrdinalIgnoreCase)
                || head.PaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(
                PitstopVoidSaleResult.Success(
                    stockRestored,
                    decimal.Round(head.Total, 2, MidpointRounding.AwayFromZero),
                    isSquare,
                    head.PitstopEodBatchId is not null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(PitstopVoidSaleResult.Fail(ex.Message));
        }
    }

    private static DateTimeOffset ParseOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.MinValue;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto;
        }

        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    private sealed class ItemStockRow
    {
        public int StockQty { get; set; }

        public int TrackStock { get; set; }

        public int OrderInMerchandise { get; set; }
    }

    private sealed class PitstopTotalsAggRow
    {
        public decimal CashTotal { get; set; }

        public decimal CardChargedTotal { get; set; }

        public decimal CardBaseProductTotal { get; set; }

        public decimal CardSurchargeCollected { get; set; }
    }

    private sealed class PitstopActiveSaleDbRow
    {
        public long SaleId { get; init; }

        public string SoldAtIso { get; init; } = string.Empty;

        public decimal Total { get; init; }

        public string PaymentMethod { get; init; } = string.Empty;

        public string? StaffDisplayName { get; init; }

        public string? SquareExternalRef { get; init; }

        public string Status { get; init; } = "Active";

        public int StockWasDeducted { get; init; }
    }

    private sealed class PitstopVoidHeadRow
    {
        public long SaleId { get; init; }

        public string Status { get; init; } = string.Empty;

        public decimal Total { get; init; }

        public string PaymentMethod { get; init; } = string.Empty;

        public int StockWasDeducted { get; init; }

        public long? PitstopEodBatchId { get; init; }

        public string? SaleGuid { get; init; }

        public string? SquareExternalRef { get; init; }
    }

    private sealed class PitstopVoidLineRow
    {
        public long ItemId { get; init; }

        public int Quantity { get; init; }
    }

    private sealed class PitstopCardSaleDbRow
    {
        public long SaleId { get; init; }

        public string? SaleRef { get; init; }

        public decimal Total { get; init; }

        public string? SquareExternalRef { get; init; }

        public string SoldAtIso { get; init; } = string.Empty;
    }
}
