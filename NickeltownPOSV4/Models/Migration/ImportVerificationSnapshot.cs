using System.Collections.Generic;

namespace NickeltownPOSV4.Models.Migration;

/// <summary>Post-import counts from SQLite plus run-level segment stats for operator verification.</summary>
public sealed class ImportVerificationSnapshot
{
    public int TabsOpenCount { get; init; }

    public int TabsArchivedCount { get; init; }

    public int TabEntriesCount { get; init; }

    public int ItemsCount { get; init; }

    public int BartendersCount { get; init; }

    public int MembersCount { get; init; }

    public int PitstopSalesCount { get; init; }

    /// <summary>Sum of <see cref="MigrationSegmentResult.Imported"/> across segments for this run.</summary>
    public int RunRecordsImported { get; init; }

    /// <summary>Files skipped because fingerprint matched a prior successful import.</summary>
    public int RunFilesSkippedDuplicate { get; init; }

    /// <summary>Parse/import failures for individual records plus global failures.</summary>
    public int RunFailedRecords { get; init; }

    public IReadOnlyList<ImportedTabVerificationRow> SampleTabs { get; init; } = [];

    public IReadOnlyList<ImportedItemVerificationRow> SampleItems { get; init; } = [];
}

/// <summary>Sample row for post-import item/stock verification UI.</summary>
public sealed class ImportedItemVerificationRow
{
    public required string Name { get; init; }

    public required string ItemType { get; init; }

    public int StockQty { get; init; }

    public required string LegacyId { get; init; }

    public required string DetailLine { get; init; }
}

public sealed class ImportedTabVerificationRow
{
    public required string TabName { get; init; }

    public decimal Balance { get; init; }

    public required string BalanceText { get; init; }

    public required string LastActivity { get; init; }

    public required string MemberOrGuest { get; init; }

    public required string OpenOrArchived { get; init; }

    public required string MemberAndStatusLine { get; init; }

    public required string LegacyId { get; init; }

    public required string LegacyKey { get; init; }

    public required string LegacyLine { get; init; }
}
