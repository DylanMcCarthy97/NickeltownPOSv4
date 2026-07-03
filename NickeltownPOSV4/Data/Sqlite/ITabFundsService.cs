using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class TabFundsCommitResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>When <see cref="Ok"/> is true, links <c>MoneyMovements</c>, <c>TabEntries</c>, and optional <c>Payments</c> for <c>ReverseFundCommitAsync</c>.</summary>
    public string? FundCommitBatchId { get; init; }

    public static TabFundsCommitResult Success(string? fundCommitBatchId = null) =>
        new() { Ok = true, FundCommitBatchId = fundCommitBatchId };

    public static TabFundsCommitResult Fail(string message) => new() { Ok = false, ErrorMessage = message };
}

/// <summary>Records club-tab money movements and updates running balance (V4 SQLite).</summary>
public interface ITabFundsService
{
    /// <summary>UI keys: cash, square, raffle, reimburse, manual, correction.</summary>
    Task<TabFundsCommitResult> CommitFundMovementAsync(
        string tabLegacyId,
        string movementUiKey,
        decimal amount,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// After Square Terminal COMPLETED. Credits tab <paramref name="baseAmountCredited"/>; stores card metadata for audit/reconciliation.
    /// </summary>
    Task<TabFundsCommitResult> CommitSquareCardTopUpAsync(
        string tabLegacyId,
        decimal baseAmountCredited,
        string? note,
        SquarePaymentCommitMetadata square,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the fund movement rows for <paramref name="commitBatchId"/> and reverses the tab balance delta.</summary>
    Task<TabFundsCommitResult> ReverseFundCommitAsync(
        string tabLegacyId,
        string commitBatchId,
        CancellationToken cancellationToken = default);
}
