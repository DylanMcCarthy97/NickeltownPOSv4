using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Dapper;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Models;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class SqliteTabRepository : ITabMigrationRepository, ITabWorkspaceQuery
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteTabRepository(SqliteConnectionFactory factory) => _factory = factory;

    public Task ImportTabsAsync(IReadOnlyList<LegacyTabDto> tabs, CancellationToken cancellationToken = default)
    {
        using var conn = _factory.OpenConnection();
        using var tx = conn.BeginTransaction();
        foreach (var dto in tabs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpsertTab(conn, tx, dto);
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    private static void UpsertTab(
        Microsoft.Data.Sqlite.SqliteConnection conn,
        Microsoft.Data.Sqlite.SqliteTransaction tx,
        LegacyTabDto dto)
    {
        var legacyId = string.IsNullOrWhiteSpace(dto.Id) ? LegacyStableId.ForTab(dto) : dto.Id!;
        var name = dto.Name ?? dto.DisplayName ?? "Tab";
        var display = dto.DisplayName ?? dto.Name ?? name;
        var balance = dto.TabBalance ?? dto.Balance ?? 0m;
        var archived = dto.Archived ?? dto.IsArchived ?? false;
        var memberId = dto.MemberId;
        var isGuest = InferLegacyGuestTab(dto, name);
        var isMember = !isGuest;
        var tabType = isGuest ? "Guest" : "Member";
        var raw = JsonSerializer.Serialize(dto);

        var history = (dto.History ?? Enumerable.Empty<LegacyTabHistoryEntryDto>())
            .Concat(dto.TabHistory ?? Enumerable.Empty<LegacyTabHistoryEntryDto>())
            .ToList();

        var lastSummary = history.Count > 0
            ? FormatLastActivitySummary(history[^1])
            : "No drinks yet";

        conn.Execute(
            """
            INSERT INTO Tabs (
              LegacyId, LegacyKey, Name, DisplayName, Balance, MemberId, IsMember, IsGuest, TabType,
              IsArchived, IsClosed, IsDeleted, LastDrinkSummary, RawJson, CreatedAt, UpdatedAt)
            VALUES (
              @LegacyId, @LegacyKey, @Name, @DisplayName, @Balance, @MemberId, @IsMember, @IsGuest, @TabType,
              @IsArchived, 0, 0, @LastDrinkSummary, @RawJson, datetime('now'), datetime('now'))
            ON CONFLICT(LegacyId) DO UPDATE SET
              Name = excluded.Name,
              DisplayName = excluded.DisplayName,
              Balance = excluded.Balance,
              MemberId = excluded.MemberId,
              IsMember = excluded.IsMember,
              IsGuest = excluded.IsGuest,
              TabType = excluded.TabType,
              IsArchived = excluded.IsArchived,
              LastDrinkSummary = excluded.LastDrinkSummary,
              RawJson = excluded.RawJson,
              UpdatedAt = datetime('now')
            """,
            new
            {
                LegacyId = legacyId,
                LegacyKey = legacyId,
                Name = name,
                DisplayName = display,
                Balance = balance,
                MemberId = memberId,
                IsMember = isMember ? 1 : 0,
                IsGuest = isGuest ? 1 : 0,
                TabType = tabType,
                IsArchived = archived ? 1 : 0,
                LastDrinkSummary = lastSummary,
                RawJson = raw,
            },
            tx);

        var tabPk = conn.QuerySingle<long>(
            "SELECT Id FROM Tabs WHERE LegacyId = @l",
            new { l = legacyId },
            tx);

        conn.Execute(
            "DELETE FROM TabEntries WHERE TabId = @t",
            new { t = tabPk },
            tx);

        for (var hi = 0; hi < history.Count; hi++)
        {
            var h = history[hi];
            var entryRaw = JsonSerializer.Serialize(h);
            var entryLegacy = string.IsNullOrWhiteSpace(h.Id)
                ? LegacyStableId.ForTabHistoryEntry(legacyId, hi, h)
                : h.Id;
            conn.Execute(
                """
                INSERT INTO TabEntries (TabId, LegacyEntryId, EntryType, Amount, Note, OccurredAt, RawJson, CreatedAt)
                VALUES (@TabId, @LegacyEntryId, @EntryType, @Amount, @Note, @OccurredAt, @RawJson, datetime('now'))
                """,
                new
                {
                    TabId = tabPk,
                    LegacyEntryId = entryLegacy,
                    EntryType = h.Type,
                    Amount = h.Amount,
                    Note = h.Note,
                    OccurredAt = h.Timestamp,
                    RawJson = entryRaw,
                },
                tx);
        }
    }

    public Task<IReadOnlyList<TabCardModel>> GetOpenTabCardsAsync(CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                using var conn = _factory.OpenConnection();
                var rows = conn.Query<TabReadRow>(
                    new CommandDefinition(
                        """
                        SELECT
                          t.Id AS TabPk,
                          t.LegacyId,
                          t.Name,
                          t.DisplayName,
                          t.Balance,
                          t.MemberId,
                          t.IsMember,
                          COALESCE(t.IsGuest, 0) AS IsGuest,
                          t.LastDrinkSummary,
                          t.LastActivityAt,
                          COALESCE(d.OpenDrinkCount, 0) AS OpenDrinkCount
                        FROM Tabs t
                        LEFT JOIN (
                          SELECT TabId, COUNT(*) AS OpenDrinkCount
                          FROM TabEntries
                          WHERE EntryType = 'Drink'
                          GROUP BY TabId
                        ) d ON d.TabId = t.Id
                        WHERE t.IsArchived = 0
                          AND COALESCE(t.IsDeleted, 0) = 0
                          AND COALESCE(t.IsClosed, 0) = 0
                        ORDER BY COALESCE(t.IsGuest, 0) ASC, t.DisplayName, t.Name
                        """,
                        cancellationToken: cancellationToken));

                var list = new List<TabCardModel>();
                foreach (var r in rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var legacy = r.LegacyId ?? string.Empty;
                    var label = r.DisplayName ?? r.Name ?? legacy;
                    var last = r.LastDrinkSummary ?? "No drinks yet";
                    var id = string.IsNullOrWhiteSpace(legacy) ? TabBoardRoute.ForSqlitePrimaryKey(r.TabPk) : legacy.Trim();
                    list.Add(new TabCardModel(id, label, r.Balance, last, r.IsMember != 0, r.IsGuest != 0, r.LastActivityAt, (int)r.OpenDrinkCount));
                }

                return (IReadOnlyList<TabCardModel>)list;
            },
            cancellationToken);

    private static string FormatLastActivitySummary(LegacyTabHistoryEntryDto h)
    {
        var note = (h.Note ?? string.Empty).Trim();
        if (note.Length > 0)
        {
            return note.Length > 96 ? note[..96] + "…" : note;
        }

        var type = (h.Type ?? "entry").Trim();
        if (h.Amount is { } amt)
        {
            var line = $"{type} {amt.ToString(CultureInfo.InvariantCulture)}".Trim();
            return string.IsNullOrEmpty(line) ? "Activity" : line;
        }

        return string.IsNullOrEmpty(type) ? "Activity" : type;
    }

    private static bool InferLegacyGuestTab(LegacyTabDto dto, string nameForHeuristic)
    {
        if (dto.IsGuest == true || dto.Guest == true)
        {
            return true;
        }

        var tt = (dto.TabType ?? string.Empty).Trim();
        if (tt.Equals("Guest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (dto.IsGuest == false || dto.Guest == false)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(dto.MemberId))
        {
            return false;
        }

        return NameLooksLikeGuestTab(nameForHeuristic);
    }

    private static bool NameLooksLikeGuestTab(string raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return false;
        }

        if (s.StartsWith("Guest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (s.StartsWith("Visitor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (s.StartsWith("Event", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return GuestBarNumber.IsMatch(s);
    }

    private static readonly Regex GuestBarNumber = new(
        @"^guest\s*bar\s*(\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed class TabReadRow
    {
        public long TabPk { get; set; }

        public string? LegacyId { get; set; }

        public string? Name { get; set; }

        public string? DisplayName { get; set; }

        public decimal Balance { get; set; }

        public string? MemberId { get; set; }

        public int IsMember { get; set; }

        public int IsGuest { get; set; }

        public string? LastDrinkSummary { get; set; }

        public string? LastActivityAt { get; set; }

        public long OpenDrinkCount { get; set; }
    }
}
