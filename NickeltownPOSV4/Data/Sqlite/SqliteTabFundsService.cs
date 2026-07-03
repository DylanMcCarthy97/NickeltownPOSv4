using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteTabFundsService : ITabFundsService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ISquarePaymentAttemptRepository _paymentAttempts;

    public SqliteTabFundsService(
        SqliteConnectionFactory factory,
        ISquarePaymentAttemptRepository paymentAttempts)
    {
        _factory = factory;
        _paymentAttempts = paymentAttempts;
    }

    public Task<TabFundsCommitResult> CommitFundMovementAsync(
        string tabLegacyId,
        string movementUiKey,
        decimal amount,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId))
        {
            return Task.FromResult(TabFundsCommitResult.Fail("No tab is selected."));
        }

        var key = (movementUiKey ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key))
        {
            return Task.FromResult(TabFundsCommitResult.Fail("Choose a transaction type."));
        }

        if (amount == 0m)
        {
            return Task.FromResult(TabFundsCommitResult.Fail("Enter a non-zero amount."));
        }

        if ((key is "cash" or "raffle" or "reimburse") && amount < 0m)
        {
            return Task.FromResult(TabFundsCommitResult.Fail("This transaction type requires a positive amount."));
        }

        var movementType = MapMovementType(key);
        if (movementType is null)
        {
            return Task.FromResult(TabFundsCommitResult.Fail("Unknown transaction type."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var tabPk = ResolveOpenTabRowId(conn, tx, tabLegacyId, cancellationToken);

            if (tabPk is null or 0)
            {
                tx.Rollback();
                return Task.FromResult(TabFundsCommitResult.Fail("Tab was not found or is archived."));
            }

            var tabPkValue = tabPk.Value;
            var fundCommitBatchId = Guid.NewGuid().ToString("N");
            var delta = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            var raw = JsonSerializer.Serialize(new
            {
                tabLegacyId,
                movementUiKey = key,
                amount = delta,
                note = trimmedNote,
                fundCommitBatchId,
            });

            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO MoneyMovements (TabId, MemberId, Amount, MovementType, Note, OccurredAt, RawJson, CreatedAt, CommitBatchId)
                    VALUES (@TabId, NULL, @Amount, @MovementType, @Note, @OccurredAt, @RawJson, datetime('now'), @CommitBatchId)
                    """,
                    new
                    {
                        TabId = tabPkValue,
                        Amount = delta,
                        MovementType = movementType,
                        Note = trimmedNote,
                        OccurredAt = stamp,
                        RawJson = raw,
                        CommitBatchId = fundCommitBatchId,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs
                    SET Balance = Balance + @delta,
                        LastDrinkSummary = @activity,
                        LastActivityAt = @OccurredAt,
                        UpdatedAt = datetime('now')
                    WHERE Id = @tabId
                    """,
                    new
                    {
                        delta,
                        activity = BuildActivitySummary(key, delta, trimmedNote),
                        tabId = tabPkValue,
                        OccurredAt = stamp,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            InsertFundTabEntry(
                conn,
                tx,
                tabPkValue,
                tabLegacyId,
                key,
                delta,
                trimmedNote,
                stamp,
                fundCommitBatchId,
                cancellationToken);

            tx.Commit();
            return Task.FromResult(TabFundsCommitResult.Success(fundCommitBatchId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabFundsCommitResult.Fail(ex.Message));
        }
    }

    public async Task<TabFundsCommitResult> CommitSquareCardTopUpAsync(
        string tabLegacyId,
        decimal baseAmountCredited,
        string? note,
        SquarePaymentCommitMetadata square,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId))
        {
            return TabFundsCommitResult.Fail("No tab is selected.");
        }

        if (baseAmountCredited <= 0m)
        {
            return TabFundsCommitResult.Fail("Enter a positive amount.");
        }

        if (string.IsNullOrWhiteSpace(square.IdempotencyKey))
        {
            return TabFundsCommitResult.Fail("Missing idempotency key.");
        }

        var idempotencyKey = square.IdempotencyKey.Trim();
        var squarePaymentId = string.IsNullOrWhiteSpace(square.SquarePaymentId)
            ? idempotencyKey
            : square.SquarePaymentId.Trim();

        long? localPaymentIdForAttempt = null;
        string? committedFundBatchId = null;

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var dupKey = conn.ExecuteScalar<long>(
                new CommandDefinition(
                    "SELECT COUNT(1) FROM Payments WHERE ExternalRef = @ExternalRef",
                    new { ExternalRef = idempotencyKey },
                    tx,
                    cancellationToken: cancellationToken));

            var dupPayment = conn.ExecuteScalar<long>(
                new CommandDefinition(
                    """
                    SELECT COUNT(1) FROM Payments
                    WHERE SquarePaymentId = @pid AND trim(COALESCE(SquarePaymentId,'')) != ''
                    """,
                    new { pid = squarePaymentId },
                    tx,
                    cancellationToken: cancellationToken));

            if (dupKey > 0 || dupPayment > 0)
            {
                tx.Commit();
                return TabFundsCommitResult.Success();
            }

            var tabPk = ResolveOpenTabRowId(conn, tx, tabLegacyId, cancellationToken);

            if (tabPk is null or 0)
            {
                tx.Rollback();
                return TabFundsCommitResult.Fail("Tab was not found or is archived.");
            }

            var tabPkValue = tabPk.Value;
            var fundCommitBatchId = Guid.NewGuid().ToString("N");
            var delta = decimal.Round(baseAmountCredited, 2, MidpointRounding.AwayFromZero);
            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            var payRaw = JsonSerializer.Serialize(new
            {
                squarePaymentId,
                squareCheckoutId = square.SquareCheckoutId,
                idempotencyKey,
                baseAmount = square.BaseAmount,
                surchargeAmount = square.SurchargeAmount,
                chargedAmount = square.ChargedAmount,
                note = trimmedNote,
                fundCommitBatchId,
            });

            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO Payments (
                      TabId, Amount, Method, ExternalRef, CreatedAt, RawJson, CommitBatchId,
                      SquarePaymentId, SquareCheckoutId, BaseAmount, SurchargeAmount, ChargedAmount)
                    VALUES (
                      @TabId, @Amount, 'Square', @ExternalRef, datetime('now'), @RawJson, @CommitBatchId,
                      @SquarePaymentId, @SquareCheckoutId, @BaseAmount, @SurchargeAmount, @ChargedAmount)
                    """,
                    new
                    {
                        TabId = tabPkValue,
                        Amount = delta,
                        ExternalRef = idempotencyKey,
                        RawJson = payRaw,
                        CommitBatchId = fundCommitBatchId,
                        SquarePaymentId = squarePaymentId,
                        SquareCheckoutId = string.IsNullOrWhiteSpace(square.SquareCheckoutId) ? null : square.SquareCheckoutId.Trim(),
                        BaseAmount = decimal.Round(square.BaseAmount, 2, MidpointRounding.AwayFromZero),
                        SurchargeAmount = decimal.Round(square.SurchargeAmount, 2, MidpointRounding.AwayFromZero),
                        ChargedAmount = decimal.Round(square.ChargedAmount, 2, MidpointRounding.AwayFromZero),
                    },
                    tx,
                    cancellationToken: cancellationToken));

            var localPaymentId = conn.ExecuteScalar<long>(
                new CommandDefinition(
                    "SELECT last_insert_rowid();",
                    transaction: tx,
                    cancellationToken: cancellationToken));

            var mmRaw = JsonSerializer.Serialize(new
            {
                tabLegacyId,
                amount = delta,
                squarePaymentId,
                idempotencyKey,
                note = trimmedNote,
                fundCommitBatchId,
            });

            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO MoneyMovements (TabId, MemberId, Amount, MovementType, Note, OccurredAt, RawJson, CreatedAt, CommitBatchId)
                    VALUES (@TabId, NULL, @Amount, @MovementType, @Note, @OccurredAt, @RawJson, datetime('now'), @CommitBatchId)
                    """,
                    new
                    {
                        TabId = tabPkValue,
                        Amount = delta,
                        MovementType = "SquareCardTopUp",
                        Note = trimmedNote,
                        OccurredAt = stamp,
                        RawJson = mmRaw,
                        CommitBatchId = fundCommitBatchId,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs
                    SET Balance = Balance + @delta,
                        LastDrinkSummary = @activity,
                        LastActivityAt = @OccurredAt,
                        UpdatedAt = datetime('now')
                    WHERE Id = @tabId
                    """,
                    new
                    {
                        delta,
                        activity = BuildActivitySummary("square", delta, trimmedNote),
                        tabId = tabPkValue,
                        OccurredAt = stamp,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            InsertFundTabEntry(
                conn,
                tx,
                tabPkValue,
                tabLegacyId,
                "square",
                delta,
                trimmedNote,
                stamp,
                fundCommitBatchId,
                cancellationToken);

            tx.Commit();
            localPaymentIdForAttempt = localPaymentId;
            committedFundBatchId = fundCommitBatchId;
        }
        catch (Exception ex)
        {
            return TabFundsCommitResult.Fail(ex.Message);
        }

        if (square.PaymentAttemptId is > 0)
        {
            try
            {
                var marked = await _paymentAttempts
                    .MarkCompletedAsync(
                        square.PaymentAttemptId.Value,
                        squarePaymentId,
                        localPaymentIdForAttempt,
                        null,
                        null,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!marked)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[TabFunds] MarkCompletedAsync returned false for attempt {square.PaymentAttemptId.Value} (Square {squarePaymentId}). Square Recovery should surface this.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TabFunds] MarkCompletedAsync failed for attempt {square.PaymentAttemptId.Value}: {ex.Message}");
            }
        }

        return committedFundBatchId is not null
            ? TabFundsCommitResult.Success(committedFundBatchId)
            : TabFundsCommitResult.Success();
    }

    public Task<TabFundsCommitResult> ReverseFundCommitAsync(
        string tabLegacyId,
        string commitBatchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabLegacyId))
        {
            return Task.FromResult(TabFundsCommitResult.Fail("No tab is selected."));
        }

        if (string.IsNullOrWhiteSpace(commitBatchId))
        {
            return Task.FromResult(TabFundsCommitResult.Fail("Nothing to reverse for that fund movement."));
        }

        var batch = commitBatchId.Trim();

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();

            var tabPk = ResolveOpenTabRowId(conn, tx, tabLegacyId, cancellationToken);

            if (tabPk is null or 0)
            {
                tx.Rollback();
                return Task.FromResult(TabFundsCommitResult.Fail("Tab was not found, is archived, or was removed."));
            }

            var tabPkValue = tabPk.Value;

            var mmRows = conn.Query<(long Id, double Amount)>(
                    new CommandDefinition(
                        """
                        SELECT mm.Id, mm.Amount
                        FROM MoneyMovements mm
                        WHERE mm.TabId = @tabId AND mm.CommitBatchId = @batch
                        """,
                        new { tabId = tabPkValue, batch },
                        tx,
                        cancellationToken: cancellationToken))
                .AsList();

            var teIds = conn.Query<long>(
                    new CommandDefinition(
                        """
                        SELECT te.Id
                        FROM TabEntries te
                        WHERE te.TabId = @tabId AND te.CommitBatchId = @batch
                        """,
                        new { tabId = tabPkValue, batch },
                        tx,
                        cancellationToken: cancellationToken))
                .AsList();

            var payIds = conn.Query<long>(
                    new CommandDefinition(
                        """
                        SELECT p.Id
                        FROM Payments p
                        WHERE p.TabId = @tabId AND p.CommitBatchId = @batch
                        """,
                        new { tabId = tabPkValue, batch },
                        tx,
                        cancellationToken: cancellationToken))
                .AsList();

            if (mmRows.Count == 0 && teIds.Count == 0 && payIds.Count == 0)
            {
                tx.Rollback();
                return Task.FromResult(TabFundsCommitResult.Fail(
                    "That fund movement was not found (or was recorded before batch undo was available)."));
            }

            if (mmRows.Count != 1 || teIds.Count != 1)
            {
                tx.Rollback();
                return Task.FromResult(TabFundsCommitResult.Fail("Stored fund movement data is inconsistent; undo was cancelled."));
            }

            if (payIds.Count > 1)
            {
                tx.Rollback();
                return Task.FromResult(TabFundsCommitResult.Fail("Stored fund movement data is inconsistent; undo was cancelled."));
            }

            var delta = decimal.Round((decimal)mmRows[0].Amount, 2, MidpointRounding.AwayFromZero);

            foreach (var pid in payIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                conn.Execute(
                    new CommandDefinition(
                        "DELETE FROM Payments WHERE Id = @id",
                        new { id = pid },
                        tx,
                        cancellationToken: cancellationToken));
            }

            conn.Execute(
                new CommandDefinition(
                    "DELETE FROM MoneyMovements WHERE Id = @id",
                    new { id = mmRows[0].Id },
                    tx,
                    cancellationToken: cancellationToken));

            conn.Execute(
                new CommandDefinition(
                    "DELETE FROM TabEntries WHERE Id = @id",
                    new { id = teIds[0] },
                    tx,
                    cancellationToken: cancellationToken));

            var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs
                    SET Balance = Balance - @delta,
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
                        delta,
                        tabId = tabPkValue,
                        stamp,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            tx.Commit();
            return Task.FromResult(TabFundsCommitResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabFundsCommitResult.Fail(ex.Message));
        }
    }

    private static long? ResolveOpenTabRowId(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        Microsoft.Data.Sqlite.SqliteTransaction? tx,
        string boardTabId,
        CancellationToken cancellationToken)
    {
        if (!TabBoardRoute.TryParse(boardTabId.Trim(), out var routeLegacy, out var routePk))
        {
            return null;
        }

        return conn.QuerySingleOrDefault<long?>(
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
    }

    private static string? MapMovementType(string key) =>
        key switch
        {
            "cash" => "CashTopUp",
            "raffle" => "RaffleWinnings",
            "reimburse" => "Reimbursement",
            "manual" => "ManualAdjustment",
            "correction" => "Correction",
            _ => null,
        };

    private static string BuildActivitySummary(string key, decimal delta, string? note)
    {
        var abs = Math.Abs(delta).ToString("0.00", CultureInfo.InvariantCulture);
        var money = (delta >= 0 ? "+$" : "-$") + abs;
        var label = key switch
        {
            "cash" => "Cash top-up",
            "square" => "Square card",
            "raffle" => "Raffle",
            "reimburse" => "Reimbursement",
            "manual" => "Manual adjustment",
            "correction" => "Correction",
            _ => "Funds",
        };

        var line = $"{label} {money}";
        if (!string.IsNullOrEmpty(note))
        {
            var shortNote = note.Length > 48 ? note[..48] + "…" : note;
            line += $" — {shortNote}";
        }

        return line.Length > 120 ? line[..120] + "…" : line;
    }

    /// <summary>Tab history panel reads <c>TabEntries</c>; mirror each fund post there.</summary>
    private static void InsertFundTabEntry(
        SqliteConnection conn,
        SqliteTransaction tx,
        long tabPkValue,
        string tabLegacyId,
        string movementUiKey,
        decimal delta,
        string? trimmedNote,
        string stamp,
        string fundCommitBatchId,
        CancellationToken cancellationToken)
    {
        var key = (movementUiKey ?? string.Empty).Trim().ToLowerInvariant();
        var entryType = TabHistoryEntryTypeLabel(key);
        var legacyEntryId = "v4f_" + Guid.NewGuid().ToString("N");
        var note = string.IsNullOrEmpty(trimmedNote)
            ? BuildActivitySummary(key, delta, null)
            : trimmedNote.Trim();

        var raw = JsonSerializer.Serialize(new
        {
            tabLegacyId,
            movementUiKey = key,
            amount = delta,
            note = trimmedNote,
            fundCommitBatchId,
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
                    EntryType = entryType,
                    Amount = delta,
                    Note = note,
                    OccurredAt = stamp,
                    RawJson = raw,
                    CommitBatchId = fundCommitBatchId,
                },
                tx,
                cancellationToken: cancellationToken));
    }

    private static string TabHistoryEntryTypeLabel(string key) =>
        key switch
        {
            "cash" => "Cash top-up",
            "square" => "Square card top-up",
            "raffle" => "Raffle winnings",
            "reimburse" => "Reimbursement",
            "manual" => "Manual adjustment",
            "correction" => "Correction",
            _ => "Funds",
        };
}
