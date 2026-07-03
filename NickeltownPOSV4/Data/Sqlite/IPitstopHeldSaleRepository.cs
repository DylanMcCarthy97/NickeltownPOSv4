using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Data.Sqlite;

public sealed class PitstopHeldSaleLineWrite
{
    public long ItemId { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public string? Sku { get; init; }

    public string? CategoryName { get; init; }

    public string? SubCategory { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }
}

public sealed class PitstopHeldSaleSummaryRow
{
    public long Id { get; init; }

    public DateTimeOffset HeldAt { get; init; }

    public int LineCount { get; init; }

    public decimal TotalAmount { get; init; }

    public string? StaffDisplayName { get; init; }
}

public sealed class PitstopHeldSaleLineRow
{
    public long ItemId { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public string? Sku { get; init; }

    public string? CategoryName { get; init; }

    public string? SubCategory { get; init; }

    public decimal UnitPrice { get; init; }

    public int Quantity { get; init; }
}

public sealed class PitstopHeldSaleDetail
{
    public long Id { get; init; }

    public IReadOnlyList<PitstopHeldSaleLineRow> Lines { get; init; } = Array.Empty<PitstopHeldSaleLineRow>();
}

/// <summary>Persists in-progress Pitstop retail carts for later recall (local terminal only).</summary>
public interface IPitstopHeldSaleRepository
{
    Task<int> GetHeldSaleCountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PitstopHeldSaleSummaryRow>> ListHeldSalesAsync(CancellationToken cancellationToken = default);

    Task<long> SaveHeldSaleAsync(
        IReadOnlyList<PitstopHeldSaleLineWrite> lines,
        long? staffId,
        string? staffDisplayName,
        CancellationToken cancellationToken = default);

    Task<PitstopHeldSaleDetail?> GetHeldSaleAsync(long heldSaleId, CancellationToken cancellationToken = default);

    Task DeleteHeldSaleAsync(long heldSaleId, CancellationToken cancellationToken = default);
}
