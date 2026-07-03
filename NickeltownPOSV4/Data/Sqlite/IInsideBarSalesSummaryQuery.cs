using System;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Aggregates tab / funds activity for Pitstop end-of-day (inside venue POS).</summary>
public interface IInsideBarSalesSummaryQuery
{
    Task<InsideBarSalesSummary> GetSummaryAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        CancellationToken cancellationToken = default);
}

public sealed class InsideBarSalesSummary
{
    /// <summary>Guest / tab cash top-ups (MoneyMovements CashTopUp).</summary>
    public decimal CashTopUps { get; init; }

    /// <summary>Square card top-ups recorded on tabs (<c>Payments</c>).</summary>
    public decimal SquarePayments { get; init; }
}
