using System;
using System.Net.Http;

namespace NickeltownPOSV4.Services;

/// <summary>
/// Maps exceptions and failures to short staff-facing text.
/// Raw exception details belong in logs, not on the till.
/// </summary>
public static class PosUserMessage
{
    public const string Generic = "Something went wrong. Try again.";
    public const string Cancelled = "Cancelled.";
    public const string NoPermission = "You do not have permission for that action.";
    public const string Network = "Network error. Check your connection and try again.";
    public const string TimedOut = "Timed out. Try again.";
    public const string CardReader = "The card reader didn't finish. Try again or use another payment.";
    public const string CardNeedsChecking = "This card payment didn't finish. Do not run the card again.";
    public const string CardApprovedUnsaved = "The card went through, but we couldn't save the sale. Do not charge them again.";
    public const string AskManager = "Try again. If it keeps happening, tell a manager.";

    public static string CouldNot(string action) =>
        $"We couldn't {action}. Try again.";

    public static string CouldNotLoad(string what) =>
        CouldNot($"load {what}");

    public static string CouldNotSave(string what) =>
        CouldNot($"save {what}");

    public static string OrFallback(string? message, string fallback) =>
        IsOperatorSafe(message) ? message!.Trim() : fallback;

    public static string FromException(Exception ex, string? fallback = null)
    {
        var safeFallback = string.IsNullOrWhiteSpace(fallback) ? Generic : fallback;
        return ex switch
        {
            OperationCanceledException => Cancelled,
            UnauthorizedAccessException => NoPermission,
            HttpRequestException => Network,
            TimeoutException => TimedOut,
            InvalidOperationException or ArgumentException =>
                IsOperatorSafe(ex.Message) ? ex.Message.Trim() : safeFallback,
            _ => safeFallback,
        };
    }

    public static bool IsOperatorSafe(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var text = message.Trim();
        if (text.Length > 180)
        {
            return false;
        }

        if (LooksTechnical(text))
        {
            return false;
        }

        return true;
    }

    public static bool LooksTechnical(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (var marker in TechnicalMarkers)
        {
            if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] TechnicalMarkers =
    [
        "sqlite",
        "exception",
        "stack trace",
        "hresult",
        "0x",
        "sql error",
        "sql logic",
        "at system.",
        "inner exception",
        "microsoft.",
        "system.io",
        "system.data",
        "constraint failed",
        "foreign key",
        "unique constraint",
        "null reference",
        "object reference",
        "not set to an instance",
        "parameter '",
        "value cannot be null",
        "json",
        "msix",
        "http/",
        "status code",
        "deadlock",
        "timeout expired",
        "see the inner",
        "unable to open database",
        "database is locked",
        "disk i/o",
        "i/o error",
        "access token",
        "access_token",
        "square api",
        "idempotency",
        "checkout id",
    ];
}
