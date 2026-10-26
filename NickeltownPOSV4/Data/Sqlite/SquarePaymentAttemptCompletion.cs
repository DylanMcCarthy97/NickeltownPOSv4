using System;
using System.Globalization;
using System.Threading;
using Dapper;
using Microsoft.Data.Sqlite;

namespace NickeltownPOSV4.Data.Sqlite;

/// <summary>Links a confirmed charge in the same transaction that saves its local money record.</summary>
internal static class SquarePaymentAttemptCompletion
{
    public static void Complete(SqliteConnection conn, SqliteTransaction tx, long? attemptId,
        string? paymentId, string? key, long? localPaymentId, long? saleId, string? saleGuid,
        CancellationToken cancellationToken)
    {
        if (attemptId is not > 0) return; // Cash and legacy imports have no Square attempt.
        var changed = conn.Execute(new CommandDefinition("""
            UPDATE SquarePaymentAttempts
            SET Status = 'Completed', LocalPaymentId = COALESCE(@localPaymentId, LocalPaymentId),
                PitstopSaleId = COALESCE(@saleId, PitstopSaleId), PitstopSaleGuid = COALESCE(@saleGuid, PitstopSaleGuid),
                UpdatedAt = @now
            WHERE Id = @attemptId AND IdempotencyKey = @key AND SquarePaymentId = @paymentId
              AND Status IN ('TerminalApproved', 'Approved', 'Completed')
              AND PaymentType = @paymentType
              AND (LocalPaymentId IS NULL OR LocalPaymentId = @localPaymentId)
              AND (PitstopSaleId IS NULL OR PitstopSaleId = @saleId)
            """, new
        {
            attemptId, paymentId, key, localPaymentId, saleId, saleGuid,
            paymentType = localPaymentId.HasValue ? "TabTopUp" : "PitstopSale",
            now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        }, tx, cancellationToken: cancellationToken));
        if (changed != 1)
            throw new InvalidOperationException("The approved Square payment could not be linked. No local changes were saved. Open Admin → Square Recovery.");
    }
}
