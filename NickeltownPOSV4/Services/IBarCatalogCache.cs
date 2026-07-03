using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.Services;

public sealed class BarCatalogSnapshot
{
    public required IReadOnlyList<string> CategoryNames { get; init; }

    public required IReadOnlyList<BarCatalogProductRow> Products { get; init; }
}

/// <summary>In-memory bar catalog snapshot for fast Add Drinks reopen.</summary>
public interface IBarCatalogCache
{
    void Invalidate();

    Task<BarCatalogSnapshot> GetOrLoadAsync(CancellationToken cancellationToken = default);
}