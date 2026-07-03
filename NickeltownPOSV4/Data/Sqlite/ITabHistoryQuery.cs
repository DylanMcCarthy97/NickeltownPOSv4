using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class TabHistoryEntryRow
{
    public string? OccurredAt { get; set; }

    public string? CreatedAt { get; set; }

    public string? EntryType { get; set; }

    public decimal? Amount { get; set; }

    public string? Note { get; set; }

    /// <summary>Optional JSON payload (V4 drink lines, imported V2 ledger rows, etc.).</summary>
    public string? RawJson { get; set; }
}

public interface ITabHistoryQuery
{
    Task<IReadOnlyList<TabHistoryEntryRow>> GetTabEntriesAsync(string tabLegacyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tab ledger lines for <paramref name="tabLegacyId"/> where the effective timestamp
    /// (<c>OccurredAt</c> or <c>CreatedAt</c>) falls in <c>[startInclusive, endExclusive)</c> (UTC instants).
    /// </summary>
    Task<IReadOnlyList<TabHistoryEntryRow>> GetTabEntriesInRangeAsync(
        string tabLegacyId,
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default);
}
