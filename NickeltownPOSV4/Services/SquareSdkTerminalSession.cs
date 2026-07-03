using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Services.Settings;
using Square;
using Square.Terminal.Checkouts;

namespace NickeltownPOSV4.Services;

/// <summary>
/// Sends card totals to a configured Square Terminal device and polls until completion (POSBarV2-equivalent flow).
/// Uses Square .NET SDK v43+ (Fern-generated client).
/// </summary>
public sealed class SquareSdkTerminalSession : ISquareTerminalSession
{
    private static readonly Regex Whitespace = new(@"[\s\r\n\t]+", RegexOptions.Compiled);

    private readonly ISquareConfigService _config;

    public SquareSdkTerminalSession(ISquareConfigService config) => _config = config;

    public Task<SquarePresentResult> PresentCardChargeAsync(
        decimal amount,
        CancellationToken cancellationToken = default,
        string? checkoutNote = null)
    {
        var request = new SquarePaymentRequest
        {
            TotalAmount = amount,
            Note = checkoutNote ?? string.Empty,
            ReferenceId = "NickeltownPOSV4",
        };
        return PresentPaymentRequestAsync(request, cancellationToken);
    }

    public async Task<SquarePresentResult> PresentPaymentRequestAsync(
        SquarePaymentRequest paymentRequest,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null)
    {
        if (paymentRequest.TotalAmount <= 0m)
        {
            return SquarePresentResult.Declined("Amount must be positive.");
        }

        var cfg = await _config.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(cfg.AccessToken)
            || string.IsNullOrWhiteSpace(cfg.DeviceId))
        {
            return SquarePresentResult.Declined("Square is not configured (access token or device id missing).");
        }

        var rounded = decimal.Round(paymentRequest.TotalAmount, 2, MidpointRounding.AwayFromZero);
        var cents = (long)(rounded * 100m);
        if (cents <= 0 || cents > 99_999_999)
        {
            return SquarePresentResult.Declined("Invalid amount for Square Terminal.");
        }

        var isSandbox = string.Equals(cfg.Environment?.Trim(), "sandbox", StringComparison.OrdinalIgnoreCase);
        var baseUrl = isSandbox ? SquareEnvironment.Sandbox : SquareEnvironment.Production;
        var client = new SquareClient(
            cfg.AccessToken.Trim(),
            new ClientOptions { BaseUrl = baseUrl });

        var orderId = paymentRequest.OrderId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(orderId) && paymentRequest.LineItems.Count > 0)
        {
            try
            {
                orderId = await CreateSquareOrderAsync(client, cfg, paymentRequest.LineItems, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Square order create failed; using amount-only checkout: {ex.Message}");
                orderId = string.Empty;
            }
        }

        var referenceId = SanitizeReferenceId(
            string.IsNullOrWhiteSpace(paymentRequest.ReferenceId) ? "NickeltownPOSV4" : paymentRequest.ReferenceId);
        var note = BuildCheckoutNote(paymentRequest, rounded);

        var deviceOptions = new DeviceCheckoutOptions
        {
            DeviceId = cfg.DeviceId.Trim(),
        };
        if (!string.IsNullOrEmpty(orderId))
        {
            deviceOptions.ShowItemizedCart = true;
        }

        var checkout = new TerminalCheckout
        {
            AmountMoney = new Money { Amount = cents, Currency = Currency.Aud },
            ReferenceId = referenceId,
            Note = note,
            DeviceOptions = deviceOptions,
            OrderId = string.IsNullOrEmpty(orderId) ? null : orderId,
        };

        var checkoutIdempotency = string.IsNullOrWhiteSpace(idempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : idempotencyKey.Trim();

        CreateTerminalCheckoutResponse createResp;
        try
        {
            createResp = await client.Terminal.Checkouts.CreateAsync(
                new CreateTerminalCheckoutRequest
                {
                    IdempotencyKey = checkoutIdempotency,
                    Checkout = checkout,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (SquareApiException ex)
        {
            return SquarePresentResult.Declined($"Square API error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SquarePresentResult.Declined($"Square request failed: {ex.Message}");
        }

        if (createResp.Errors != null && createResp.Errors.Any())
        {
            var msg = string.Join("; ", createResp.Errors.Select(e => $"{e.Code}: {e.Detail}"));
            return SquarePresentResult.Declined(msg);
        }

        var checkoutId = createResp.Checkout?.Id;
        if (string.IsNullOrWhiteSpace(checkoutId))
        {
            return SquarePresentResult.Declined("Square did not return a checkout id.");
        }

        try
        {
            for (var i = 0; i < 40; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var check = await client.Terminal.Checkouts.GetAsync(
                        new GetCheckoutsRequest { CheckoutId = checkoutId },
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (check.Errors != null && check.Errors.Any())
                    {
                        var pollMsg = string.Join("; ", check.Errors.Select(e => $"{e.Code}: {e.Detail}"));
                        return SquarePresentResult.Declined(pollMsg);
                    }

                    var status = check.Checkout?.Status ?? string.Empty;
                    if (string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                    {
                        var paymentId = check.Checkout?.PaymentIds?.FirstOrDefault() ?? checkoutId;
                        return SquarePresentResult.ApprovedSim(paymentId, checkoutId, checkoutIdempotency);
                    }

                    if (string.Equals(status, "CANCELED", StringComparison.OrdinalIgnoreCase))
                    {
                        return SquarePresentResult.Declined("Payment cancelled on Square Terminal.", cancelled: true);
                    }

                    if (string.Equals(status, "FAILED", StringComparison.OrdinalIgnoreCase))
                    {
                        return SquarePresentResult.Declined("Square Terminal reported payment failed.");
                    }
                }
                catch (SquareApiException)
                {
                }

                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            }

            await TryCancelCheckoutAsync(client, checkoutId, cancellationToken).ConfigureAwait(false);
            return SquarePresentResult.Declined("Square Terminal did not complete within 2 minutes.", timedOut: true);
        }
        catch (OperationCanceledException)
        {
            await TryCancelCheckoutAsync(client, checkoutId, CancellationToken.None).ConfigureAwait(false);
            return SquarePresentResult.Declined("Card payment cancelled.", cancelled: true);
        }
    }

    private static async Task<string> CreateSquareOrderAsync(
        SquareClient client,
        Models.Settings.AppSquareConfig cfg,
        IReadOnlyList<SquareTerminalLineItem> lineItems,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cfg.LocationId))
        {
            throw new InvalidOperationException("Square location id is not configured.");
        }

        var orderLineItems = new List<OrderLineItem>();
        foreach (var item in lineItems)
        {
            if (item.Quantity <= 0)
            {
                continue;
            }

            var itemName = item.Name;
            if (!string.IsNullOrWhiteSpace(item.Category))
            {
                itemName = string.IsNullOrWhiteSpace(item.Note)
                    ? $"{item.Name} ({item.Category})"
                    : $"{item.Name} ({item.Category} - {item.Note})";
            }
            else if (!string.IsNullOrWhiteSpace(item.Note))
            {
                itemName = $"{item.Name} ({item.Note})";
            }

            var unitCents = (long)(decimal.Round(item.UnitPrice, 2, MidpointRounding.AwayFromZero) * 100m);
            OrderLineItem orderLineItem;
            if (!string.IsNullOrWhiteSpace(item.CatalogObjectId))
            {
                orderLineItem = new OrderLineItem
                {
                    Quantity = item.Quantity.ToString(),
                    Name = itemName,
                    CatalogObjectId = item.CatalogObjectId.Trim(),
                    BasePriceMoney = new Money { Amount = unitCents, Currency = Currency.Aud },
                };
            }
            else
            {
                orderLineItem = new OrderLineItem
                {
                    Quantity = item.Quantity.ToString(),
                    Name = itemName,
                    BasePriceMoney = new Money { Amount = unitCents, Currency = Currency.Aud },
                };
            }

            orderLineItems.Add(orderLineItem);
        }

        if (orderLineItems.Count == 0)
        {
            throw new InvalidOperationException("No line items to create a Square order.");
        }

        var createResp = await client.Orders.CreateAsync(
            new CreateOrderRequest
            {
                IdempotencyKey = Guid.NewGuid().ToString(),
                Order = new Order
                {
                    LocationId = cfg.LocationId.Trim(),
                    LineItems = orderLineItems,
                },
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (createResp.Errors != null && createResp.Errors.Any())
        {
            var msg = string.Join("; ", createResp.Errors.Select(e => $"{e.Code}: {e.Detail}"));
            throw new InvalidOperationException(msg);
        }

        var orderId = createResp.Order?.Id;
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new InvalidOperationException("Square did not return an order id.");
        }

        return orderId;
    }

    private static string BuildCheckoutNote(SquarePaymentRequest paymentRequest, decimal roundedAmount)
    {
        var note = paymentRequest.Note?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(note) && paymentRequest.LineItems.Count > 0)
        {
            note = string.Join(
                ", ",
                paymentRequest.LineItems.Select(i => $"{i.Name} x{i.Quantity} ${decimal.Round(i.UnitPrice * i.Quantity, 2, MidpointRounding.AwayFromZero):0.00}"));
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            note = $"Charge {roundedAmount:0.00} AUD";
        }

        note = note.Replace("\u2013", "-").Replace("\u2014", "-");
        if (note.Length > 500)
        {
            note = note[..497] + "...";
        }

        return note;
    }

    private static async Task TryCancelCheckoutAsync(
        SquareClient client,
        string checkoutId,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.Terminal.Checkouts.CancelAsync(
                new CancelCheckoutsRequest { CheckoutId = checkoutId },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static string SanitizeReferenceId(string raw)
    {
        var s = string.IsNullOrWhiteSpace(raw) ? "NickeltownPOSV4" : raw.Trim();
        s = Whitespace.Replace(s, string.Empty);
        return s.Length > 40 ? s[..40] : s;
    }
}
