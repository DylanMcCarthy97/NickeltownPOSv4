using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Models;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteTabManagementRepository : ITabManagementRepository
{
    private static readonly Regex GuestSequenceNumber = new(
        @"^guest\s*(\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly SqliteConnectionFactory _factory;

    public SqliteTabManagementRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task<bool> ExistsOpenTabDisplayNameAsync(
        string displayName,
        string? exceptLegacyId,
        CancellationToken cancellationToken = default,
        long? exceptSqliteTabId = null)
    {
        var n = NormalizeName(displayName);
        if (string.IsNullOrEmpty(n))
        {
            return Task.FromResult(false);
        }

        using var conn = _factory.OpenConnection();
        var exLeg = string.IsNullOrWhiteSpace(exceptLegacyId) ? null : exceptLegacyId.Trim();
        var count = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*) FROM Tabs
                WHERE COALESCE(IsDeleted,0) = 0 AND IsArchived = 0 AND COALESCE(IsClosed,0) = 0
                  AND (lower(trim(COALESCE(DisplayName,''))) = @n OR lower(trim(Name)) = @n)
                  AND NOT (
                    (@exTab IS NOT NULL AND Id = @exTab)
                    OR (@exLeg IS NOT NULL AND LegacyId IS NOT NULL AND LegacyId = @exLeg)
                  )
                """,
                new { n, exLeg, exTab = exceptSqliteTabId },
                cancellationToken: cancellationToken));
        return Task.FromResult(count > 0);
    }

    public Task<bool> ExistsOpenMemberTabDisplayNameAsync(
        string displayName,
        string? exceptLegacyId,
        CancellationToken cancellationToken = default,
        long? exceptSqliteTabId = null)
    {
        var n = NormalizeName(displayName);
        if (string.IsNullOrEmpty(n))
        {
            return Task.FromResult(false);
        }

        using var conn = _factory.OpenConnection();
        var exLeg = string.IsNullOrWhiteSpace(exceptLegacyId) ? null : exceptLegacyId.Trim();
        var count = conn.ExecuteScalar<long>(
            new CommandDefinition(
                """
                SELECT COUNT(*) FROM Tabs
                WHERE COALESCE(IsDeleted,0) = 0 AND IsArchived = 0 AND COALESCE(IsClosed,0) = 0
                  AND COALESCE(IsGuest,0) = 0
                  AND (lower(trim(COALESCE(DisplayName,''))) = @n OR lower(trim(Name)) = @n)
                  AND NOT (
                    (@exTab IS NOT NULL AND Id = @exTab)
                    OR (@exLeg IS NOT NULL AND LegacyId IS NOT NULL AND LegacyId = @exLeg)
                  )
                """,
                new { n, exLeg, exTab = exceptSqliteTabId },
                cancellationToken: cancellationToken));
        return Task.FromResult(count > 0);
    }

    public async Task<string> SuggestNextGuestSequenceNameAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var names = conn.Query<string>(
            new CommandDefinition(
                """
                SELECT COALESCE(DisplayName, Name)
                FROM Tabs
                WHERE COALESCE(IsDeleted,0) = 0
                """,
                cancellationToken: cancellationToken)).ToList();

        var max = 0;
        foreach (var raw in names)
        {
            var t = (raw ?? string.Empty).Trim();
            var m = GuestSequenceNumber.Match(t);
            if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
            {
                max = Math.Max(max, num);
            }
        }

        for (var i = max + 1; i < max + 10000; i++)
        {
            var candidate = $"Guest {i}";
            if (!await ExistsOpenTabDisplayNameAsync(candidate, null, cancellationToken).ConfigureAwait(false))
            {
                return candidate;
            }
        }

        return $"Guest {Guid.NewGuid().ToString("N")[..6]}";
    }

    public async Task<TabMutationResult> CreateTabAsync(
        string displayName,
        PosTabAccountKind kind,
        decimal startingBalance,
        string? memberId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var label = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(label))
        {
            return TabMutationResult.Fail("Enter a tab name.");
        }

        MapKind(kind, out var isMember, out var isGuest);
        var dup = kind == PosTabAccountKind.Member
            ? await ExistsOpenMemberTabDisplayNameAsync(label, null, cancellationToken).ConfigureAwait(false)
            : await ExistsOpenTabDisplayNameAsync(label, null, cancellationToken).ConfigureAwait(false);
        if (dup)
        {
            return TabMutationResult.Fail(
                kind == PosTabAccountKind.Member
                    ? "That member tab name is already in use."
                    : "That tab name is already in use.");
        }

        var member = (memberId ?? string.Empty).Trim();
        if (kind == PosTabAccountKind.Member && string.IsNullOrEmpty(member))
        {
            member = null;
        }

        var tabType = TabTypeFromKind(kind, isGuest);
        var legacyId = "v4t_" + Guid.NewGuid().ToString("N");
        var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var bal = decimal.Round(startingBalance, 2, MidpointRounding.AwayFromZero);
        var raw = JsonSerializer.Serialize(new { v = 4, legacyId, label, kind = kind.ToString(), tabType, bal });

        try
        {
            using var conn = _factory.OpenConnection();
            using var tx = conn.BeginTransaction();
            conn.Execute(
                new CommandDefinition(
                    """
                    INSERT INTO Tabs (
                      LegacyId, LegacyKey, Name, DisplayName, Balance, MemberId, IsMember, IsGuest, TabType,
                      IsArchived, IsDeleted, IsClosed, Notes, LastDrinkSummary, LastActivityAt, RawJson, CreatedAt, UpdatedAt)
                    VALUES (
                      @LegacyId, @LegacyKey, @Name, @DisplayName, @Balance, @MemberId, @IsMember, @IsGuest, @TabType,
                      0, 0, 0, @Notes, @LastDrinkSummary, @LastActivityAt, @RawJson, datetime('now'), datetime('now'))
                    """,
                    new
                    {
                        LegacyId = legacyId,
                        LegacyKey = legacyId,
                        Name = label,
                        DisplayName = label,
                        Balance = bal,
                        MemberId = string.IsNullOrEmpty(member) ? null : member,
                        IsMember = isMember ? 1 : 0,
                        IsGuest = isGuest ? 1 : 0,
                        TabType = tabType,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                        LastDrinkSummary = bal != 0m ? "Opening balance" : "No drinks yet",
                        LastActivityAt = stamp,
                        RawJson = raw,
                    },
                    tx,
                    cancellationToken: cancellationToken));

            if (bal != 0m)
            {
                var tabPk = conn.QuerySingle<long>(
                    "SELECT Id FROM Tabs WHERE LegacyId = @l",
                    new { l = legacyId },
                    tx);
                var entryId = "v4e_" + Guid.NewGuid().ToString("N");
                conn.Execute(
                    new CommandDefinition(
                        """
                        INSERT INTO TabEntries (TabId, LegacyEntryId, EntryType, Amount, Note, OccurredAt, RawJson, CreatedAt)
                        VALUES (@TabId, @LegacyEntryId, @EntryType, @Amount, @Note, @OccurredAt, @RawJson, datetime('now'))
                        """,
                        new
                        {
                            TabId = tabPk,
                            LegacyEntryId = entryId,
                            EntryType = "opening_balance",
                            Amount = bal,
                            Note = "Opening balance",
                            OccurredAt = stamp,
                            RawJson = JsonSerializer.Serialize(new { opening = true, amount = bal }),
                        },
                        tx,
                        cancellationToken: cancellationToken));
            }

            tx.Commit();
            return TabMutationResult.Success(legacyId);
        }
        catch (Exception ex)
        {
            return TabMutationResult.Fail(ex.Message);
        }
    }

    public Task<TabEditorRow?> GetTabEditorRowAsync(string legacyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyId))
        {
            return Task.FromResult<TabEditorRow?>(null);
        }

        if (!TabBoardRoute.TryParse(legacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult<TabEditorRow?>(null);
        }

        using var conn = _factory.OpenConnection();
        var row = conn.QuerySingleOrDefault<EditorSqlRow>(
            new CommandDefinition(
                """
                SELECT Id AS TabPk, LegacyId, DisplayName, Name, IsMember, IsGuest, Notes, MemberId
                FROM Tabs
                WHERE COALESCE(IsDeleted,0) = 0
                  AND ((@LegacyId IS NOT NULL AND LegacyId = @LegacyId) OR (@SqliteTabId IS NOT NULL AND Id = @SqliteTabId))
                LIMIT 1
                """,
                new { LegacyId = routeLegacy, SqliteTabId = routePk },
                cancellationToken: cancellationToken));
        if (row is null)
        {
            return Task.FromResult<TabEditorRow?>(null);
        }

        var stableKey = string.IsNullOrWhiteSpace(row.LegacyId)
            ? TabBoardRoute.ForSqlitePrimaryKey(row.TabPk)
            : row.LegacyId.Trim();
        var label = string.IsNullOrWhiteSpace(row.DisplayName) ? row.Name : row.DisplayName;
        return Task.FromResult<TabEditorRow?>(
            new TabEditorRow
            {
                LegacyId = stableKey,
                DisplayName = label?.Trim() ?? stableKey,
                IsMember = row.IsMember != 0,
                IsGuest = row.IsGuest != 0,
                Notes = row.Notes,
                MemberId = row.MemberId,
            });
    }

    public async Task<TabMutationResult> UpdateTabAsync(
        string legacyId,
        string displayName,
        PosTabAccountKind kind,
        string? notes,
        string? memberId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyId))
        {
            return TabMutationResult.Fail("No tab selected.");
        }

        var label = (displayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(label))
        {
            return TabMutationResult.Fail("Enter a tab name.");
        }

        if (!TabBoardRoute.TryParse(legacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return TabMutationResult.Fail("No tab selected.");
        }

        MapKind(kind, out var isMember, out var isGuest);
        var tabType = TabTypeFromKind(kind, isGuest);
        var member = (memberId ?? string.Empty).Trim();
        if (kind != PosTabAccountKind.Member)
        {
            member = null;
        }

        var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            using var conn = _factory.OpenConnection();
            var self = conn.QuerySingleOrDefault<(long Id, string? Leg)>(
                new CommandDefinition(
                    """
                    SELECT Id, LegacyId FROM Tabs
                    WHERE COALESCE(IsDeleted,0) = 0
                      AND ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                    LIMIT 1
                    """,
                    new { RouteLegacy = routeLegacy, RoutePk = routePk },
                    cancellationToken: cancellationToken));
            if (self.Id <= 0)
            {
                return TabMutationResult.Fail("Tab was not found.");
            }

            var dupEdit = kind == PosTabAccountKind.Member
                ? await ExistsOpenMemberTabDisplayNameAsync(label, self.Leg, cancellationToken, self.Id).ConfigureAwait(false)
                : await ExistsOpenTabDisplayNameAsync(label, self.Leg, cancellationToken, self.Id).ConfigureAwait(false);
            if (dupEdit)
            {
                return TabMutationResult.Fail(
                    kind == PosTabAccountKind.Member
                        ? "That member tab name is already in use."
                        : "That tab name is already in use.");
            }

            var n = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs SET
                      Name = @Name,
                      DisplayName = @DisplayName,
                      MemberId = @MemberId,
                      IsMember = @IsMember,
                      IsGuest = @IsGuest,
                      TabType = @TabType,
                      Notes = @Notes,
                      LastActivityAt = @LastActivityAt,
                      UpdatedAt = datetime('now')
                    WHERE COALESCE(IsDeleted,0) = 0
                      AND ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                    """,
                    new
                    {
                        RouteLegacy = routeLegacy,
                        RoutePk = routePk,
                        Name = label,
                        DisplayName = label,
                        MemberId = string.IsNullOrEmpty(member) ? null : member,
                        IsMember = isMember ? 1 : 0,
                        IsGuest = isGuest ? 1 : 0,
                        TabType = tabType,
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                        LastActivityAt = stamp,
                    },
                    cancellationToken: cancellationToken));
            return n == 0
                ? TabMutationResult.Fail("Tab was not found.")
                : TabMutationResult.Success(legacyId);
        }
        catch (Exception ex)
        {
            return TabMutationResult.Fail(ex.Message);
        }
    }

    public Task<TabMutationResult> SetTabArchivedAsync(string legacyId, bool archived, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyId))
        {
            return Task.FromResult(TabMutationResult.Fail("No tab id."));
        }

        if (!TabBoardRoute.TryParse(legacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult(TabMutationResult.Fail("No tab id."));
        }

        var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            using var conn = _factory.OpenConnection();
            var n = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs SET
                      IsArchived = @A,
                      IsClosed = CASE WHEN @A = 0 THEN 0 ELSE IsClosed END,
                      ClosedAt = CASE WHEN @A = 0 THEN NULL ELSE ClosedAt END,
                      ClosedByBartenderId = CASE WHEN @A = 0 THEN NULL ELSE ClosedByBartenderId END,
                      CloseReason = CASE WHEN @A = 0 THEN NULL ELSE CloseReason END,
                      LastActivityAt = @LastActivityAt,
                      UpdatedAt = datetime('now')
                    WHERE COALESCE(IsDeleted,0) = 0
                      AND ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                    """,
                    new { RouteLegacy = routeLegacy, RoutePk = routePk, A = archived ? 1 : 0, LastActivityAt = stamp },
                    cancellationToken: cancellationToken));
            return Task.FromResult(n == 0 ? TabMutationResult.Fail("Tab was not found.") : TabMutationResult.Success(legacyId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabMutationResult.Fail(ex.Message));
        }
    }

    public Task<TabMutationResult> SoftDeleteTabAsync(string legacyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyId))
        {
            return Task.FromResult(TabMutationResult.Fail("No tab id."));
        }

        if (!TabBoardRoute.TryParse(legacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult(TabMutationResult.Fail("No tab id."));
        }

        var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            using var conn = _factory.OpenConnection();
            var n = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs SET
                      IsDeleted = 1,
                      IsArchived = 0,
                      LastActivityAt = @LastActivityAt,
                      UpdatedAt = datetime('now')
                    WHERE COALESCE(IsDeleted,0) = 0
                      AND ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                    """,
                    new { RouteLegacy = routeLegacy, RoutePk = routePk, LastActivityAt = stamp },
                    cancellationToken: cancellationToken));
            return Task.FromResult(n == 0 ? TabMutationResult.Fail("Tab was not found.") : TabMutationResult.Success(legacyId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabMutationResult.Fail(ex.Message));
        }
    }

    public Task<TabMutationResult> RestoreSoftDeletedTabAsync(string legacyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyId))
        {
            return Task.FromResult(TabMutationResult.Fail("No tab id."));
        }

        if (!TabBoardRoute.TryParse(legacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult(TabMutationResult.Fail("No tab id."));
        }

        var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            using var conn = _factory.OpenConnection();
            var n = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs SET
                      IsDeleted = 0,
                      LastActivityAt = @LastActivityAt,
                      UpdatedAt = datetime('now')
                    WHERE COALESCE(IsDeleted,0) != 0
                      AND ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                    """,
                    new { RouteLegacy = routeLegacy, RoutePk = routePk, LastActivityAt = stamp },
                    cancellationToken: cancellationToken));
            return Task.FromResult(
                n == 0
                    ? TabMutationResult.Fail("Tab was not found or was not removed.")
                    : TabMutationResult.Success(legacyId));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabMutationResult.Fail(ex.Message));
        }
    }

    public Task<TabMutationResult> PermanentDeleteTabAsync(string legacyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyId))
        {
            return Task.FromResult(TabMutationResult.Fail("No tab id."));
        }

        if (!TabBoardRoute.TryParse(legacyId.Trim(), out var routeLegacy, out var routePk))
        {
            return Task.FromResult(TabMutationResult.Fail("No tab id."));
        }

        try
        {
            using var conn = _factory.OpenConnection();
            var n = conn.Execute(
                new CommandDefinition(
                    """
                    DELETE FROM Tabs
                    WHERE ((@RouteLegacy IS NOT NULL AND LegacyId = @RouteLegacy) OR (@RoutePk IS NOT NULL AND Id = @RoutePk))
                    """,
                    new { RouteLegacy = routeLegacy, RoutePk = routePk },
                    cancellationToken: cancellationToken));
            return Task.FromResult(n == 0 ? TabMutationResult.Fail("Tab was not found.") : TabMutationResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabMutationResult.Fail(ex.Message));
        }
    }

    public Task<int> CountArchivedTabsAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var n = conn.ExecuteScalar<long>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Tabs WHERE IsArchived != 0 AND COALESCE(IsDeleted,0) = 0",
                cancellationToken: cancellationToken));
        return Task.FromResult((int)n);
    }

    public Task<IReadOnlyList<ArchivedTabListItem>> GetArchivedTabsPageAsync(
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0)
        {
            pageSize = 8;
        }

        if (pageIndex < 0)
        {
            pageIndex = 0;
        }

        var skip = pageIndex * pageSize;
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<ArchivedSqlRow>(
            new CommandDefinition(
                """
                SELECT LegacyId, DisplayName, Name, Balance,
                  COALESCE(LastActivityAt, UpdatedAt, CreatedAt) AS ActivityStamp
                FROM Tabs
                WHERE IsArchived != 0 AND COALESCE(IsDeleted,0) = 0
                ORDER BY datetime(COALESCE(LastActivityAt, UpdatedAt, CreatedAt)) DESC, Id DESC
                LIMIT @Take OFFSET @Skip
                """,
                new { Take = pageSize, Skip = skip },
                cancellationToken: cancellationToken));

        var list = new List<ArchivedTabListItem>();
        foreach (var r in rows)
        {
            var label = string.IsNullOrWhiteSpace(r.DisplayName) ? r.Name : r.DisplayName;
            var stamp = r.ActivityStamp;
            var last = string.IsNullOrWhiteSpace(stamp)
                ? "—"
                : FormatActivity(stamp);
            list.Add(
                new ArchivedTabListItem(
                    r.LegacyId ?? string.Empty,
                    label?.Trim() ?? r.LegacyId ?? "?",
                    r.Balance,
                    last));
        }

        return Task.FromResult<IReadOnlyList<ArchivedTabListItem>>(list);
    }

    public Task<IReadOnlyList<GuestCloseoutRow>> GetOpenGuestTabsForCloseoutAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        var rows = conn.Query<GuestCloseoutSqlRow>(
            new CommandDefinition(
                """
                SELECT Id AS TabPk, LegacyId, DisplayName, Name, Balance, LastActivityAt, CreatedAt
                FROM Tabs
                WHERE COALESCE(IsGuest,0) != 0
                  AND COALESCE(IsDeleted,0) = 0
                  AND IsArchived = 0
                  AND COALESCE(IsClosed,0) = 0
                ORDER BY COALESCE(DisplayName, Name), LegacyId
                """,
                cancellationToken: cancellationToken));

        var list = new List<GuestCloseoutRow>();
        foreach (var r in rows)
        {
            var label = string.IsNullOrWhiteSpace(r.DisplayName) ? r.Name : r.DisplayName;
            var boardId = string.IsNullOrWhiteSpace(r.LegacyId)
                ? TabBoardRoute.ForSqlitePrimaryKey(r.TabPk)
                : r.LegacyId.Trim();
            list.Add(
                new GuestCloseoutRow
                {
                    LegacyId = boardId,
                    DisplayName = label?.Trim() ?? boardId ?? "?",
                    Balance = r.Balance,
                    LastActivityText = string.IsNullOrWhiteSpace(r.LastActivityAt) ? "—" : FormatActivity(r.LastActivityAt!),
                    CreatedText = string.IsNullOrWhiteSpace(r.CreatedAt) ? "—" : FormatActivity(r.CreatedAt!),
                });
        }

        return Task.FromResult<IReadOnlyList<GuestCloseoutRow>>(list);
    }

    public Task<TabMutationResult> CloseGuestTabsEndOfNightAsync(
        IReadOnlyList<string> legacyIds,
        long? closedByBartenderPk,
        string closeReason,
        CancellationToken cancellationToken = default)
    {
        var ids = (legacyIds ?? Array.Empty<string>())
            .Select(s => (s ?? string.Empty).Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return Task.FromResult(TabMutationResult.Fail("No guest tabs selected."));
        }

        return BulkCloseGuestsAsync(ids, closedByBartenderPk, closeReason, onlyZeroBalance: false, cancellationToken);
    }

    public Task<TabMutationResult> CloseAllZeroBalanceGuestTabsAsync(
        long? closedByBartenderPk,
        string closeReason,
        CancellationToken cancellationToken = default) =>
        BulkCloseGuestsAsync(Array.Empty<string>(), closedByBartenderPk, closeReason, onlyZeroBalance: true, cancellationToken);

    public Task<TabMutationResult> ArchiveGuestTabsAsync(IReadOnlyList<string> legacyIds, CancellationToken cancellationToken = default)
    {
        var ids = (legacyIds ?? Array.Empty<string>())
            .Select(s => (s ?? string.Empty).Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0)
        {
            return Task.FromResult(TabMutationResult.Fail("No guest tabs selected."));
        }

        var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            using var conn = _factory.OpenConnection();
            var n = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs SET
                      IsArchived = 1,
                      LastActivityAt = @LastActivityAt,
                      UpdatedAt = datetime('now')
                    WHERE LegacyId IN @Ids
                      AND COALESCE(IsGuest,0) != 0
                      AND COALESCE(IsDeleted,0) = 0
                      AND IsArchived = 0
                    """,
                    new { Ids = ids, LastActivityAt = stamp },
                    cancellationToken: cancellationToken));
            return Task.FromResult(n == 0 ? TabMutationResult.Fail("No matching open guest tabs were archived.") : TabMutationResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabMutationResult.Fail(ex.Message));
        }
    }

    public Task<TabMutationResult> ArchiveAllOpenGuestTabsAsync(CancellationToken cancellationToken = default)
    {
        var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            using var conn = _factory.OpenConnection();
            var n = conn.Execute(
                new CommandDefinition(
                    """
                    UPDATE Tabs SET
                      IsArchived = 1,
                      LastActivityAt = @LastActivityAt,
                      UpdatedAt = datetime('now')
                    WHERE COALESCE(IsGuest,0) != 0
                      AND COALESCE(IsDeleted,0) = 0
                      AND IsArchived = 0
                    """,
                    new { LastActivityAt = stamp },
                    cancellationToken: cancellationToken));
            return Task.FromResult(TabMutationResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabMutationResult.Fail(ex.Message));
        }
    }

    private Task<TabMutationResult> BulkCloseGuestsAsync(
        string[] legacyIds,
        long? closedByBartenderPk,
        string closeReason,
        bool onlyZeroBalance,
        CancellationToken cancellationToken)
    {
        var stamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var reason = string.IsNullOrWhiteSpace(closeReason) ? "End of night guest closeout" : closeReason.Trim();
        try
        {
            using var conn = _factory.OpenConnection();
            int n;
            if (onlyZeroBalance)
            {
                n = conn.Execute(
                    new CommandDefinition(
                        """
                        UPDATE Tabs SET
                          IsClosed = 1,
                          ClosedAt = @ClosedAt,
                          ClosedByBartenderId = @Who,
                          CloseReason = @Reason,
                          IsArchived = 1,
                          LastActivityAt = @ClosedAt,
                          UpdatedAt = datetime('now')
                        WHERE COALESCE(IsGuest,0) != 0
                          AND COALESCE(IsDeleted,0) = 0
                          AND IsArchived = 0
                          AND COALESCE(IsClosed,0) = 0
                          AND Balance = 0
                        """,
                        new { ClosedAt = stamp, Who = closedByBartenderPk, Reason = reason },
                        cancellationToken: cancellationToken));
            }
            else
            {
                n = conn.Execute(
                    new CommandDefinition(
                        """
                        UPDATE Tabs SET
                          IsClosed = 1,
                          ClosedAt = @ClosedAt,
                          ClosedByBartenderId = @Who,
                          CloseReason = @Reason,
                          IsArchived = 1,
                          LastActivityAt = @ClosedAt,
                          UpdatedAt = datetime('now')
                        WHERE LegacyId IN @Ids
                          AND COALESCE(IsGuest,0) != 0
                          AND COALESCE(IsDeleted,0) = 0
                          AND IsArchived = 0
                          AND COALESCE(IsClosed,0) = 0
                        """,
                        new { Ids = legacyIds, ClosedAt = stamp, Who = closedByBartenderPk, Reason = reason },
                        cancellationToken: cancellationToken));
            }

            return Task.FromResult(
                n == 0
                    ? TabMutationResult.Fail(onlyZeroBalance ? "No zero-balance guest tabs to close." : "No matching open guest tabs were closed.")
                    : TabMutationResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(TabMutationResult.Fail(ex.Message));
        }
    }

    private static string FormatActivity(string stamp)
    {
        if (DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }

        return stamp.Length > 24 ? stamp[..24] : stamp;
    }

    private static string NormalizeName(string displayName) =>
        (displayName ?? string.Empty).Trim().ToLowerInvariant();

    private static string TabTypeFromKind(PosTabAccountKind kind, bool isGuest)
    {
        if (isGuest)
        {
            return "Guest";
        }

        return kind == PosTabAccountKind.Account ? "Account" : "Member";
    }

    private static void MapKind(PosTabAccountKind kind, out bool isMember, out bool isGuest)
    {
        switch (kind)
        {
            case PosTabAccountKind.Member:
                isMember = true;
                isGuest = false;
                break;
            case PosTabAccountKind.Guest:
                isMember = false;
                isGuest = true;
                break;
            default:
                isMember = false;
                isGuest = false;
                break;
        }
    }

    private sealed class EditorSqlRow
    {
        public long TabPk { get; set; }

        public string? LegacyId { get; set; }

        public string? DisplayName { get; set; }

        public string? Name { get; set; }

        public int IsMember { get; set; }

        public int IsGuest { get; set; }

        public string? Notes { get; set; }

        public string? MemberId { get; set; }
    }

    private sealed class ArchivedSqlRow
    {
        public string? LegacyId { get; set; }

        public string? DisplayName { get; set; }

        public string? Name { get; set; }

        public decimal Balance { get; set; }

        public string? ActivityStamp { get; set; }
    }

    private sealed class GuestCloseoutSqlRow
    {
        public long TabPk { get; set; }

        public string? LegacyId { get; set; }

        public string? DisplayName { get; set; }

        public string? Name { get; set; }

        public decimal Balance { get; set; }

        public string? LastActivityAt { get; set; }

        public string? CreatedAt { get; set; }
    }
}
