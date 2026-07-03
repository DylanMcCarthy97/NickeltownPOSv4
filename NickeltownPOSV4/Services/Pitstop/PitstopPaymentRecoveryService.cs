using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Pitstop;

public interface IPitstopPaymentRecoveryService
{
    Task<SquareRecoveryUpdateResult> RecoverPitstopSaleAsync(long attemptId, CancellationToken cancellationToken = default);
    Task<SquareRecoveryUpdateResult> IgnoreAsync(long attemptId, string? note, CancellationToken cancellationToken = default);
}

public sealed class PitstopPaymentRecoveryService : IPitstopPaymentRecoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ISquareRecoveryRepository _recovery;
    private readonly IPitstopRetailSaleRepository _sales;
    private readonly ISquarePaymentAttemptRepository _attempts;
    private readonly IAuditLogService _audit;

    public PitstopPaymentRecoveryService(ISquareRecoveryRepository recovery, IPitstopRetailSaleRepository sales, ISquarePaymentAttemptRepository attempts, IAuditLogService audit)
    {
        _recovery = recovery;
        _sales = sales;
        _attempts = attempts;
        _audit = audit;
    }

    public async Task<SquareRecoveryUpdateResult> RecoverPitstopSaleAsync(long attemptId, CancellationToken cancellationToken = default)
    {
        var json = await _recovery.GetRecoveryPayloadJsonAsync(attemptId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return SquareRecoveryUpdateResult.Fail("No recoverable sale payload for this payment.");
        PitstopPaymentRecoveryPayload? payload;
        try { payload = JsonSerializer.Deserialize<PitstopPaymentRecoveryPayload>(json, JsonOptions); }
        catch { return SquareRecoveryUpdateResult.Fail("Recovery payload is invalid."); }
        if (payload is null || payload.Lines.Count == 0) return SquareRecoveryUpdateResult.Fail("Recovery payload is empty.");
        var commit = await _sales.CommitSaleAsync(payload.Lines, payload.Payment, cancellationToken).ConfigureAwait(false);
        if (!commit.Ok) return SquareRecoveryUpdateResult.Fail(commit.ErrorMessage ?? "Could not commit recovered sale.");
        if (payload.Payment.PaymentAttemptId is > 0)
            await _attempts.MarkCompletedAsync(payload.Payment.PaymentAttemptId.Value, payload.Payment.SquareExternalRef ?? payload.TransactionGuid, null, commit.SalePk, commit.SaleGuid, cancellationToken).ConfigureAwait(false);
        if (commit.SalePk is > 0)
            await _recovery.LinkPitstopSaleAsync(attemptId, commit.SalePk.Value, "Recovered from admin", cancellationToken).ConfigureAwait(false);
        await _audit.LogAsync(AuditActions.PaymentSaleSaved, AuditEntityTypes.PitstopSale, commit.SaleGuid, payload.Payment.ChargedTotal, reason: $"Recovered Pitstop sale from attempt {attemptId}.").ConfigureAwait(false);
        await _audit.LogAsync(AuditActions.StockDeducted, AuditEntityTypes.PitstopSale, commit.SaleGuid, reason: "Stock deducted during payment recovery.").ConfigureAwait(false);
        return SquareRecoveryUpdateResult.Success();
    }

    public Task<SquareRecoveryUpdateResult> IgnoreAsync(long attemptId, string? note, CancellationToken cancellationToken = default) =>
        _recovery.MarkIgnoredAsync(attemptId, note, cancellationToken);
}