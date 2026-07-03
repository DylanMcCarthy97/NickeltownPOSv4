using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Payments;
using NickeltownPOSV4.Models.Pitstop;

namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>Pitstop retail EOD reconciliation only - bar tab payments and sales are excluded.</summary>
public sealed class PitstopEodReconciliationService
{
    private readonly SqliteConnectionFactory _factory;
    private readonly IPitstopRetailSaleRepository _pitstopSales;

    public PitstopEodReconciliationService(
        SqliteConnectionFactory factory,
        IPitstopRetailSaleRepository pitstopSales)
    {
        _factory = factory;
        _pitstopSales = pitstopSales;
    }

    public async Task<PitstopEodReconciliationReport> BuildAsync(
        DateTimeOffset startInclusive,
        DateTimeOffset endExclusive,
        decimal squareFeePercent,
        CancellationToken cancellationToken = default)
    {
        var pit = await _pitstopSales
            .GetPitstopRetailPaymentTotalsAsync(startInclusive, endExclusive, cancellationToken)
            .ConfigureAwait(false);

        using var conn = _factory.OpenConnection();
        var startIso = startInclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var endIso = endExclusive.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

        var warnings = new List<string>();

        warnings.AddRange(conn.Query<string>(
            new CommandDefinition(
                """
                SELECT 'Pitstop Square approved not linked to sale: ' || IdempotencyKey
                FROM SquarePaymentAttempts
                WHERE PaymentType = @pitstop
                  AND Status = @approved
                  AND SquarePaymentId IS NOT NULL AND trim(SquarePaymentId) != ''
                  AND datetime(CreatedAt) >= datetime(@startIso)
                  AND datetime(CreatedAt) < datetime(@endIso)
                """,
                new
                {
                    pitstop = SquarePaymentAttemptType.PitstopSale,
                    approved = SquarePaymentAttemptStatus.TerminalApproved,
                    startIso,
                    endIso,
                },
                cancellationToken: cancellationToken)));

        warnings.AddRange(conn.Query<string>(
            new CommandDefinition(
                """
                SELECT PaymentType || ' Square attempt ' || Status || ': '
                  || COALESCE(FailureReason, '(no detail)') || ' [' || IdempotencyKey || ']'
                FROM SquarePaymentAttempts
                WHERE PaymentType = @pitstop
                  AND Status IN (@failed, @cancelled, @timedOut)
                  AND datetime(CreatedAt) >= datetime(@startIso)
                  AND datetime(CreatedAt) < datetime(@endIso)
                """,
                new
                {
                    pitstop = SquarePaymentAttemptType.PitstopSale,
                    failed = SquarePaymentAttemptStatus.Failed,
                    cancelled = SquarePaymentAttemptStatus.Cancelled,
                    timedOut = SquarePaymentAttemptStatus.TimedOut,
                    startIso,
                    endIso,
                },
                cancellationToken: cancellationToken)));

        warnings.AddRange(conn.Query<string>(
            new CommandDefinition(
                """
                SELECT 'Pitstop card sale missing Square reference: '
                  || COALESCE(SaleGuid, LegacyId, cast(Id as text))
                FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND COALESCE(Status,'Active') = 'Active'
                  AND lower(trim(COALESCE(PaymentMethod,''))) IN ('card','square')
                  AND (SquareExternalRef IS NULL OR trim(SquareExternalRef) = '')
                  AND datetime(COALESCE(SoldAt, CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(SoldAt, CreatedAt)) < datetime(@endIso)
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken)));

        warnings.AddRange(conn.Query<string>(
            new CommandDefinition(
                """
                SELECT 'Invalid Pitstop sale total: ' || COALESCE(SaleGuid, LegacyId, cast(Id as text))
                FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND COALESCE(Status,'Active') = 'Active'
                  AND COALESCE(Total, 0) <= 0
                  AND datetime(COALESCE(SoldAt, CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(SoldAt, CreatedAt)) < datetime(@endIso)
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken)));

        var voidedCount = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND COALESCE(Status,'Active') = 'Voided'
                  AND datetime(COALESCE(VoidedAt, SoldAt, CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(VoidedAt, SoldAt, CreatedAt)) < datetime(@endIso)
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken));

        if (voidedCount > 0)
        {
            warnings.Add($"{voidedCount} Pitstop sale(s) were voided during this period. Confirm any Square sales were refunded in Square.");
        }

        var unarchivedActive = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM PitstopSales
                WHERE lower(trim(COALESCE(SaleMode, ''))) = 'pitstop'
                  AND PitstopEodBatchId IS NULL
                  AND COALESCE(Status,'Active') = 'Active'
                  AND datetime(COALESCE(SoldAt, CreatedAt)) >= datetime(@startIso)
                  AND datetime(COALESCE(SoldAt, CreatedAt)) < datetime(@endIso)
                """,
                new { startIso, endIso },
                cancellationToken: cancellationToken));

        if (unarchivedActive > 0)
        {
            warnings.Add($"{unarchivedActive} active Pitstop sale(s) are not yet archived for this period.");
        }

        var unresolvedSquare = conn.ExecuteScalar<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1) FROM SquarePaymentAttempts
                WHERE UPPER(TRIM(COALESCE(Status,''))) IN ('TERMINALAPPROVED','COMPLETED','APPROVED')
                  AND SquarePaymentId IS NOT NULL AND trim(SquarePaymentId) != ''
                  AND PitstopSaleId IS NULL
                  AND LocalPaymentId IS NULL
                  AND UPPER(TRIM(COALESCE(RecoveryStatus,''))) NOT IN ('MANUALLYRECONCILED','LINKEDPITSTOP','LINKEDTAB','IGNORED')
                """,
                cancellationToken: cancellationToken));

        if (unresolvedSquare > 0)
        {
            warnings.Add($"{unresolvedSquare} unresolved Square payment(s) need reconciliation in Square Recovery.");
        }

        var cardCharged = pit.CardChargedTotal;
        var surcharge = pit.CardSurchargeCollected;
        var fees = decimal.Round(cardCharged * (squareFeePercent / 100m), 2, MidpointRounding.AwayFromZero);
        var totalRecorded = decimal.Round(pit.CashTotal + cardCharged, 2, MidpointRounding.AwayFromZero);

        return new PitstopEodReconciliationReport
        {
            PeriodCaption =
                $"{startInclusive.LocalDateTime:dddd d MMM yyyy} -> {endExclusive.LocalDateTime:dddd d MMM yyyy}",
            CashSales = pit.CashTotal,
            CardSalesCharged = cardCharged,
            CardBaseProductTotal = pit.CardBaseProductTotal,
            CardSurchargeCollected = surcharge,
            EstimatedSquareFee = fees,
            NetCardAfterFees = decimal.Round(cardCharged - fees, 2, MidpointRounding.AwayFromZero),
            TotalRecordedSales = totalRecorded,
            Warnings = warnings,
        };
    }
}