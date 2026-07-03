using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Audit;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Payments;

public sealed class SquareCardPaymentOrchestrator : ISquareCardPaymentOrchestrator
{
    private readonly ISquareTerminalSession _terminal;
    private readonly ISquarePaymentAttemptRepository _attempts;
    private readonly IAuditLogService _audit;

    public SquareCardPaymentOrchestrator(
        ISquareTerminalSession terminal,
        ISquarePaymentAttemptRepository attempts,
        IAuditLogService audit)
    {
        _terminal = terminal;
        _attempts = attempts;
        _audit = audit;
    }

    public async Task<SquareCardPaymentOutcome> PresentAndLogAsync(
        SquareCardPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        await LogPaymentAsync(
                AuditActions.PaymentStarted,
                idempotencyKey,
                request.ChargedAmount,
                $"Square {request.PaymentType} payment started.")
            .ConfigureAwait(false);

        var begin = await _attempts
            .BeginAsync(
                new SquarePaymentAttemptBeginRequest
                {
                    PaymentType = request.PaymentType,
                    IdempotencyKey = idempotencyKey,
                    TabLegacyId = request.TabLegacyId,
                    BaseAmount = request.BaseAmount,
                    SurchargeAmount = request.SurchargeAmount,
                    ChargedAmount = request.ChargedAmount,
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (begin.AlreadyCompleted)
        {
            await LogPaymentAsync(
                    AuditActions.SquareAttemptCompleted,
                    begin.IdempotencyKey,
                    request.ChargedAmount,
                    "Idempotent replay — attempt already completed.")
                .ConfigureAwait(false);

            return new SquareCardPaymentOutcome
            {
                Approved = true,
                AlreadyRecorded = true,
                IdempotencyKey = begin.IdempotencyKey,
                SquarePaymentId = begin.SquarePaymentId,
                SquareCheckoutId = begin.SquareCheckoutId,
                AttemptId = begin.AttemptId,
            };
        }

        if (begin.AlreadyTerminalApproved)
        {
            var paymentId = begin.SquarePaymentId ?? begin.IdempotencyKey;
            await LogPaymentAsync(
                    AuditActions.PaymentApproved,
                    paymentId,
                    request.ChargedAmount,
                    "Terminal already approved for this transaction GUID — skipping second Square call.")
                .ConfigureAwait(false);

            return new SquareCardPaymentOutcome
            {
                Approved = true,
                IdempotencyKey = begin.IdempotencyKey,
                SquarePaymentId = paymentId,
                SquareCheckoutId = begin.SquareCheckoutId,
                AttemptId = begin.AttemptId,
            };
        }

        await LogPaymentAsync(
                AuditActions.PaymentSent,
                begin.IdempotencyKey,
                request.ChargedAmount,
                "Payment request sent to Square Terminal.")
            .ConfigureAwait(false);

        await LogPaymentAsync(
                AuditActions.SquareAttemptStarted,
                begin.AttemptId.ToString(),
                request.ChargedAmount,
                $"Attempt {begin.AttemptId} pending on terminal.")
            .ConfigureAwait(false);

        SquarePresentResult present;
        try
        {
            present = await _terminal
                .PresentPaymentRequestAsync(
                    request.TerminalRequest,
                    cancellationToken,
                    begin.IdempotencyKey)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _attempts.MarkFailedAsync(begin.AttemptId, "Card payment cancelled.", CancellationToken.None)
                .ConfigureAwait(false);
            await LogPaymentAsync(
                    AuditActions.PaymentDeclined,
                    begin.IdempotencyKey,
                    request.ChargedAmount,
                    "Card payment cancelled.",
                    success: false)
                .ConfigureAwait(false);
            await LogPaymentAsync(
                    AuditActions.SquareAttemptFailed,
                    begin.AttemptId.ToString(),
                    request.ChargedAmount,
                    "Card payment cancelled.",
                    success: false)
                .ConfigureAwait(false);

            return new SquareCardPaymentOutcome
            {
                DeclineReason = "Card payment cancelled.",
                IdempotencyKey = begin.IdempotencyKey,
                AttemptId = begin.AttemptId,
            };
        }

        if (!present.Approved)
        {
            var reason = present.DeclineReason ?? "Card payment was not approved.";
            await _attempts.MarkFailedAsync(begin.AttemptId, reason, CancellationToken.None).ConfigureAwait(false);
            await LogPaymentAsync(
                    AuditActions.PaymentDeclined,
                    begin.IdempotencyKey,
                    request.ChargedAmount,
                    reason,
                    success: false)
                .ConfigureAwait(false);
            await LogPaymentAsync(
                    AuditActions.SquareAttemptFailed,
                    begin.AttemptId.ToString(),
                    request.ChargedAmount,
                    reason,
                    success: false)
                .ConfigureAwait(false);

            return new SquareCardPaymentOutcome
            {
                DeclineReason = reason,
                IdempotencyKey = begin.IdempotencyKey,
                AttemptId = begin.AttemptId,
            };
        }

        var approvedPaymentId = present.ExternalTransactionId ?? begin.IdempotencyKey;
        await _attempts
            .MarkTerminalApprovedAsync(begin.AttemptId, approvedPaymentId, present.SquareCheckoutId, CancellationToken.None)
            .ConfigureAwait(false);

        await LogPaymentAsync(
                AuditActions.PaymentApproved,
                approvedPaymentId,
                request.ChargedAmount,
                "Square Terminal approved the payment.")
            .ConfigureAwait(false);
        await LogPaymentAsync(
                AuditActions.SquareAttemptApproved,
                begin.AttemptId.ToString(),
                request.ChargedAmount,
                $"Attempt {begin.AttemptId} terminal approved.")
            .ConfigureAwait(false);

        return new SquareCardPaymentOutcome
        {
            Approved = true,
            IdempotencyKey = present.IdempotencyKey ?? begin.IdempotencyKey,
            SquarePaymentId = approvedPaymentId,
            SquareCheckoutId = present.SquareCheckoutId,
            AttemptId = begin.AttemptId,
        };
    }

    private Task LogPaymentAsync(
        string action,
        string? entityId,
        decimal amount,
        string reason,
        bool success = true) =>
        _audit.LogAsync(
            action,
            AuditEntityTypes.SquareAttempt,
            entityId,
            amount,
            reason,
            success);
}
