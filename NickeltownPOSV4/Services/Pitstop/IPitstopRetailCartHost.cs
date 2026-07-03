using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Pitstop;

public sealed class PitstopHeldSaleRecallResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public static PitstopHeldSaleRecallResult Success() => new() { Ok = true };

    public static PitstopHeldSaleRecallResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}

/// <summary>Allows the held-sales panel to restore a parked Pitstop cart into the active register.</summary>
public interface IPitstopRetailCartHost
{
    bool HasActiveCart { get; }

    Task RefreshHeldSaleCountAsync(CancellationToken cancellationToken = default);

    Task<PitstopHeldSaleRecallResult> RecallHeldSaleAsync(long heldSaleId, CancellationToken cancellationToken = default);
}
