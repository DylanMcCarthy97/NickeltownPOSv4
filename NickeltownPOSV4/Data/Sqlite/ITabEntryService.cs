using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class TabDrinkSaleLine
{
    public long ItemId { get; init; }

    public required string DisplayName { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }
}

public sealed class TabDrinkCommitResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>When <see cref="Ok"/> is true, identifies the inserted drink lines for <c>ReverseDrinkBatchAsync</c>.</summary>
    public string? DrinkCommitBatchId { get; init; }

    public static TabDrinkCommitResult Success(string? drinkCommitBatchId = null) =>
        new() { Ok = true, DrinkCommitBatchId = drinkCommitBatchId };

    public static TabDrinkCommitResult Fail(string message) => new() { Ok = false, ErrorMessage = message };
}

/// <summary>
/// Posts drink lines to a tab in one transaction: TabEntries, balance -= line totals (V2-style running tab balance),
/// last drink summary and last activity timestamp, optional <c>Items.StockQty</c> decrement plus <c>StockMovements</c> when <c>TrackStock</c> is on.
/// </summary>
public interface ITabEntryService
{
    Task<TabDrinkCommitResult> CommitDrinkSaleAsync(
        string tabLegacyId,
        IReadOnlyList<TabDrinkSaleLine> lines,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes drink <c>TabEntries</c> for <paramref name="commitBatchId"/>, refunds tab balance, restores stock and removes matching <c>StockMovements</c> when applicable.
    /// </summary>
    Task<TabDrinkCommitResult> ReverseDrinkBatchAsync(
        string tabLegacyId,
        string commitBatchId,
        CancellationToken cancellationToken = default);
}
