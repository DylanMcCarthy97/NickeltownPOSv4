using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public static class ComplimentaryTransactionTypes
{
    public const string ComplimentaryItem = "ComplimentaryItem";
}

public static class ComplimentaryIssueStatus
{
    public const string Active = "Active";
    public const string Reversed = "Reversed";
}

public sealed class ComplimentaryRecordRequest
{
    public long ItemId { get; init; }

    public int Quantity { get; init; } = 1;

    public long? StaffId { get; init; }

    public string? StaffName { get; init; }

    public string? IdempotencyKey { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }
}

public sealed class ComplimentaryRecordResult
{
    public bool Ok { get; init; }

    public bool AlreadyRecorded { get; init; }

    public string? ErrorMessage { get; init; }

    public string? IssueGuid { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public int StockQtyAfter { get; init; }

    public static ComplimentaryRecordResult Success(string issueGuid, string itemName, int quantity, int stockQtyAfter, bool alreadyRecorded = false) =>
        new()
        {
            Ok = true,
            AlreadyRecorded = alreadyRecorded,
            IssueGuid = issueGuid,
            ItemName = itemName,
            Quantity = quantity,
            StockQtyAfter = stockQtyAfter,
        };

    public static ComplimentaryRecordResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

public sealed class ComplimentaryReverseResult
{
    public bool Ok { get; init; }

    public bool AlreadyReversed { get; init; }

    public string? ErrorMessage { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public static ComplimentaryReverseResult Success(string itemName, int quantity, bool alreadyReversed = false) =>
        new()
        {
            Ok = true,
            AlreadyReversed = alreadyReversed,
            ItemName = itemName,
            Quantity = quantity,
        };

    public static ComplimentaryReverseResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

public sealed class QuickFreeButtonRow
{
    public long ItemId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string DisplayLabel { get; init; } = string.Empty;

    public string? Icon { get; init; }

    public int SortOrder { get; init; }

    public int StockQty { get; init; }

    public int TrackStock { get; init; }

    public int TodayCount { get; init; }

    public decimal UnitRetailPrice { get; init; }
}

public sealed class QuickFreeConfigRow
{
    public long Id { get; init; }

    public long ItemId { get; init; }

    public string ProductName { get; init; } = string.Empty;

    public string? DisplayLabel { get; init; }

    public string? Icon { get; init; }

    public int SortOrder { get; init; }

    public int ProductIsActive { get; init; }
}

public sealed class QuickFreeProductCandidate
{
    public long ItemId { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal PitstopPrice { get; init; }

    public int StockQty { get; init; }
}

public sealed class ComplimentaryReportLine
{
    public long ItemId { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public decimal UnitRetailPrice { get; init; }

    public decimal RetailValue { get; init; }
}

public sealed class ComplimentaryReport
{
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public IReadOnlyList<ComplimentaryReportLine> Lines { get; init; } = [];

    public int TotalItems { get; init; }

    public decimal TotalRetailValue { get; init; }
}

public sealed class ComplimentaryConfigResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public static ComplimentaryConfigResult Success() => new() { Ok = true };

    public static ComplimentaryConfigResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

public interface IComplimentaryItemRepository
{
    Task<ComplimentaryRecordResult> RecordAsync(
        ComplimentaryRecordRequest request,
        CancellationToken cancellationToken = default);

    Task<ComplimentaryReverseResult> ReverseAsync(
        string issueGuid,
        long? staffId,
        string? staffName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickFreeButtonRow>> GetButtonsAsync(
        DateOnly localDate,
        CancellationToken cancellationToken = default);

    Task<int> GetTodayCountAsync(
        long itemId,
        DateOnly localDate,
        CancellationToken cancellationToken = default);

    Task<ComplimentaryReport> GetReportAsync(
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickFreeConfigRow>> GetConfigAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickFreeProductCandidate>> GetProductCandidatesAsync(CancellationToken cancellationToken = default);

    Task<ComplimentaryConfigResult> AddConfigAsync(
        long itemId,
        string? displayLabel,
        string? icon,
        CancellationToken cancellationToken = default);

    Task<ComplimentaryConfigResult> RemoveConfigAsync(
        long itemId,
        CancellationToken cancellationToken = default);

    Task<ComplimentaryConfigResult> UpdateConfigAsync(
        long itemId,
        string? displayLabel,
        string? icon,
        CancellationToken cancellationToken = default);

    Task<ComplimentaryConfigResult> MoveConfigAsync(
        long itemId,
        int direction,
        CancellationToken cancellationToken = default);
}
