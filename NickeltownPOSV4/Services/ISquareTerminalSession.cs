using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

public sealed class SquareTerminalLineItem
{
    public string Name { get; init; } = string.Empty;

    public int Quantity { get; init; } = 1;

    public decimal UnitPrice { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public string CatalogObjectId { get; init; } = string.Empty;
}

public sealed class SquarePaymentRequest
{
    public decimal TotalAmount { get; init; }

    public string Note { get; init; } = string.Empty;

    public string ReferenceId { get; init; } = string.Empty;

    public IReadOnlyList<SquareTerminalLineItem> LineItems { get; init; } = [];

    public string OrderId { get; init; } = string.Empty;
}

public sealed class SquarePresentResult
{
    public bool Approved { get; init; }

    public string? ExternalTransactionId { get; init; }

    public string? SquareCheckoutId { get; init; }

    public string? IdempotencyKey { get; init; }

    public string? DeclineReason { get; init; }

    public bool TimedOut { get; init; }

    public bool Cancelled { get; init; }

    public static SquarePresentResult ApprovedSim(string externalId, string? checkoutId = null, string? idempotencyKey = null) =>
        new()
        {
            Approved = true,
            ExternalTransactionId = externalId,
            SquareCheckoutId = checkoutId,
            IdempotencyKey = idempotencyKey,
        };

    public static SquarePresentResult Declined(string reason, bool timedOut = false, bool cancelled = false) =>
        new()
        {
            Approved = false,
            DeclineReason = reason,
            TimedOut = timedOut,
            Cancelled = cancelled,
        };
}

/// <summary>
/// Presents a card charge to Square Terminal via the Square .NET SDK (production or sandbox from settings).
/// </summary>
public interface ISquareTerminalSession
{
    /// <param name="checkoutNote">Optional note on the Square Terminal checkout (shown on device / in dashboard).</param>
    Task<SquarePresentResult> PresentCardChargeAsync(
        decimal amount,
        CancellationToken cancellationToken = default,
        string? checkoutNote = null);

    /// <summary>POSBarV2 <c>SendToSquareTerminalWithPaymentRequest</c> — optional order + itemized cart when line items are supplied.</summary>
    Task<SquarePresentResult> PresentPaymentRequestAsync(
        SquarePaymentRequest paymentRequest,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null);
}
