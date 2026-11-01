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
    private readonly IAuditLogService _audit;

    public PitstopPaymentRecoveryService(ISquareRecoveryRepository recovery, IPitstopRetailSaleRepository sales, ISquarePaymentAttemptRepository attempts, IAuditLogService audit)
    {
        _recovery = recovery;
        _sales = sales;
        _audit = audit;
    }

    public async Task<SquareRecoveryUpdateResult> RecoverPitstopSaleAsync(long attemptId, CancellationToken cancellationToken = default)
    {
        var rows = await _recovery.GetOrphanAttemptsAsync(cancellationToken).ConfigureAwait(false);
        var row = System.Linq.Enumerable.FirstOrDefault(rows, r => r.AttemptId == attemptId);
        if (row is null)
            return SquareRecoveryUpdateResult.Fail("This payment is already cleared.");
        if (row.Status is not ("TerminalApproved" or "Completed" or "Approved"))
            return SquareRecoveryUpdateResult.Fail("Find out if they paid first. Do not charge them again.");
        var json = await _recovery.GetRecoveryPayloadJsonAsync(attemptId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return SquareRecoveryUpdateResult.Fail("This till cannot rebuild the sale. Write down the amount. Do not take cash for this same sale.");
        PitstopPaymentRecoveryPayload? payload;
        try { payload = JsonSerializer.Deserialize<PitstopPaymentRecoveryPayload>(json, JsonOptions); }
        catch { return SquareRecoveryUpdateResult.Fail("We couldn't read that sale. Try Find out if they paid again."); }
        if (payload is null || payload.Lines.Count == 0) return SquareRecoveryUpdateResult.Fail("This till cannot rebuild the sale. Write down the amount. Do not take cash for this same sale.");
        var payment = StampApprovedRefs(payload.Payment, row);
        var commit = await _sales.CommitSaleAsync(payload.Lines, payment, cancellationToken).ConfigureAwait(false);
        if (!commit.Ok) return SquareRecoveryUpdateResult.Fail(commit.ErrorMessage ?? "We couldn't save that sale. Try Find out if they paid again.");
        if (commit.SalePk is > 0)
            await _recovery.LinkPitstopSaleAsync(attemptId, commit.SalePk.Value, "Recovered from Pitstop till", cancellationToken).ConfigureAwait(false);
        await _audit.LogAsync(AuditActions.PaymentSaleSaved, AuditEntityTypes.PitstopSale, commit.SaleGuid, payment.ChargedTotal, reason: $"Recovered Pitstop sale from attempt {attemptId}.").ConfigureAwait(false);
        await _audit.LogAsync(AuditActions.StockDeducted, AuditEntityTypes.PitstopSale, commit.SaleGuid, reason: "Stock deducted during payment recovery.").ConfigureAwait(false);
        return SquareRecoveryUpdateResult.Success();
    }

    public Task<SquareRecoveryUpdateResult> IgnoreAsync(long attemptId, string? note, CancellationToken cancellationToken = default) =>
        _recovery.MarkIgnoredAsync(attemptId, note, cancellationToken);

    private static PitstopSalePaymentCommit StampApprovedRefs(PitstopSalePaymentCommit payment, SquareRecoveryRow row) =>
        new()
        {
            PaymentMethod = string.IsNullOrWhiteSpace(payment.PaymentMethod) ? "Card" : payment.PaymentMethod,
            BartenderId = payment.BartenderId,
            StaffDisplayName = payment.StaffDisplayName,
            SquareExternalRef = FirstNonEmpty(row.SquarePaymentId, payment.SquareExternalRef),
            SquareCheckoutId = FirstNonEmpty(row.SquareCheckoutId, payment.SquareCheckoutId),
            BaseProductTotal = payment.BaseProductTotal,
            CardSurchargePercent = payment.CardSurchargePercent,
            CardSurchargeAmount = payment.CardSurchargeAmount,
            ChargedTotal = payment.ChargedTotal > 0m ? payment.ChargedTotal : row.ChargedAmount,
            IdempotencyKey = FirstNonEmpty(payment.IdempotencyKey, row.IdempotencyKey),
            CashReceived = payment.CashReceived,
            CashChange = payment.CashChange,
            PaymentAttemptId = payment.PaymentAttemptId ?? row.AttemptId,
        };

    private static string? FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
}
