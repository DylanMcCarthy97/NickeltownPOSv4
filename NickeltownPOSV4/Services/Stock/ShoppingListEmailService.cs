using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Saturday automated BAR shopping-list email only (never merchandise).</summary>
public sealed class ShoppingListEmailService : IShoppingListEmailService
{
    private readonly Data.Sqlite.IStockEditingService _stock;
    private readonly IEmailConfigService _emailConfig;
    private readonly IEmailSender _email;
    private readonly ILogger<ShoppingListEmailService> _logger;

    public ShoppingListEmailService(
        Data.Sqlite.IStockEditingService stock,
        IEmailConfigService emailConfig,
        IEmailSender email,
        ILogger<ShoppingListEmailService> logger)
    {
        _stock = stock;
        _emailConfig = emailConfig;
        _email = email;
        _logger = logger;
    }

    public async Task<ShoppingListEmailResult> TrySendScheduledAsync(CancellationToken cancellationToken = default)
    {
        var config = await _emailConfig.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!config.ShoppingListSaturdayEmailEnabled)
        {
            return new ShoppingListEmailResult(true, "Saturday bar shopping-list email is disabled.", Skipped: true);
        }

        if (!EmailAddressValidation.IsValid(config.ShoppingListRecipientEmail))
        {
            _logger.LogWarning("Saturday bar shopping-list email enabled but recipient is missing or invalid.");
            return new ShoppingListEmailResult(false, "Shopping-list recipient email is missing or invalid.");
        }

        if (!SaturdayEmailClaim.TryClaim(SaturdayEmailClaim.ShoppingListKind))
        {
            return new ShoppingListEmailResult(true, "Bar shopping-list email already claimed for today.", Skipped: true);
        }

        try
        {
            var result = await SendCoreAsync(config, isTest: false, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded && !result.Skipped)
            {
                SaturdayEmailClaim.ReleaseIfSendFailed(SaturdayEmailClaim.ShoppingListKind);
            }

            if (result.Succeeded)
            {
                _logger.LogInformation("Saturday bar shopping-list email: {Message}", result.Message);
            }
            else
            {
                _logger.LogWarning("Saturday bar shopping-list email failed: {Message}", result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            SaturdayEmailClaim.ReleaseIfSendFailed(SaturdayEmailClaim.ShoppingListKind);
            _logger.LogError(ex, "Saturday bar shopping-list email failed.");
            return new ShoppingListEmailResult(false, ex.Message);
        }
    }

    public async Task<ShoppingListEmailResult> SendTestAsync(CancellationToken cancellationToken = default)
    {
        var config = await _emailConfig.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!EmailAddressValidation.IsValid(config.ShoppingListRecipientEmail))
        {
            return new ShoppingListEmailResult(false, "Set a valid shopping-list recipient email before sending a test.");
        }

        try
        {
            var result = await SendCoreAsync(config, isTest: true, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                _logger.LogInformation("Test bar shopping-list email: {Message}", result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test bar shopping-list email failed.");
            return new ShoppingListEmailResult(false, ex.Message);
        }
    }

    private async Task<ShoppingListEmailResult> SendCoreAsync(
        AppEmailConfig config,
        bool isTest,
        CancellationToken cancellationToken)
    {
        // Same stock snapshot source as the Saturday stock report (active catalog rows).
        var stockRows = await _stock.GetStockRowsAsync(includeInactive: false, cancellationToken).ConfigureAwait(false);
        // Automated email is BAR only — merchandise is never included.
        var snapshot = ShoppingListEmailComposer.BuildSnapshot(stockRows, ShoppingListKind.Bar);
        var to = new[] { EmailAddressValidation.Normalize(config.ShoppingListRecipientEmail) };
        var dateLabel = DateTime.Now.ToString("yyyy-MM-dd");

        if (snapshot.PurchaseRows.Count == 0)
        {
            if (!isTest && config.ShoppingListSkipWhenEmpty)
            {
                return new ShoppingListEmailResult(true, "Bar shopping list empty — email skipped.", Skipped: true);
            }

            await _email.SendToAsync(
                subject: (isTest ? "[TEST] " : string.Empty)
                    + $"Nickeltown bar shopping list — clear ({dateLabel})",
                body: ShoppingListEmailComposer.BuildEmptyConfirmationBody(ShoppingListKind.Bar, isTest),
                toRecipients: to,
                attachments: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return new ShoppingListEmailResult(
                true,
                isTest
                    ? $"Test confirmation sent to {to[0]} (bar list empty)."
                    : $"Empty bar-list confirmation sent to {to[0]}.");
        }

        var pdf = ShoppingListEmailComposer.BuildPdf(snapshot);
        var fileName = $"{ShoppingListCatalog.FileStem(ShoppingListKind.Bar)}_{DateTime.Now:yyyyMMdd}.pdf";
        await _email.SendToAsync(
            subject: (isTest ? "[TEST] " : string.Empty)
                + $"Nickeltown bar shopping list — {DateTime.Now:dddd d MMMM yyyy}",
            body: ShoppingListEmailComposer.BuildEmailBody(snapshot, isTest, isSaturdaySchedule: !isTest),
            toRecipients: to,
            attachments: [new EmailAttachment(fileName, pdf, "application/pdf")],
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new ShoppingListEmailResult(
            true,
            isTest
                ? $"Test bar shopping-list email sent to {to[0]} ({snapshot.PurchaseRows.Count} products)."
                : $"Bar shopping-list email sent to {to[0]} ({snapshot.PurchaseRows.Count} products).");
    }
}
