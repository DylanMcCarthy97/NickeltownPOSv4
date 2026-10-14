using System;
using System.IO;
using System.Threading;
using NickeltownPOSV4.Services.Treasurer;

namespace NickeltownPOSV4.Services.Settings;

/// <summary>Cross-process once-per-Saturday claim flags for automated emails.</summary>
public static class SaturdayEmailClaim
{
    public const string StockKind = "stock";
    public const string ShoppingListKind = "shopping_list";

    public static bool TryClaim(string kind, DateTime? now = null)
    {
        var when = now ?? DateTime.Now;
        var flagPath = GetFlagPathOrNull(kind, when);
        if (flagPath is null)
        {
            // Non-Saturday callers (tests / manual) still allow send.
            return true;
        }

        var datePart = when.ToString("yyyy-MM-dd");
        var mutexName = @"Global\NickeltownPOSV4_SaturdayEmail_"
            + kind + "_"
            + datePart.Replace("-", string.Empty, StringComparison.Ordinal);

        try
        {
            using var mutex = new Mutex(false, mutexName);
            mutex.WaitOne();
            try
            {
                if (File.Exists(flagPath))
                {
                    return false;
                }

                File.WriteAllText(flagPath, $"{Environment.MachineName}{Environment.NewLine}{when:O}{Environment.NewLine}");
                return true;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch
        {
            return false;
        }
    }

    public static void ReleaseIfSendFailed(string kind, DateTime? now = null)
    {
        try
        {
            var flagPath = GetFlagPathOrNull(kind, now ?? DateTime.Now);
            if (string.IsNullOrEmpty(flagPath))
            {
                return;
            }

            if (File.Exists(flagPath))
            {
                File.Delete(flagPath);
            }
        }
        catch
        {
            // Best-effort; next catch-up may retry.
        }
    }

    public static bool HasClaim(string kind, DateTime? now = null)
    {
        var flagPath = GetFlagPathOrNull(kind, now ?? DateTime.Now);
        return !string.IsNullOrEmpty(flagPath) && File.Exists(flagPath);
    }

    public static string? GetFlagPathOrNull(string kind, DateTime when)
    {
        if (when.DayOfWeek != DayOfWeek.Saturday)
        {
            return null;
        }

        var datePart = when.ToString("yyyy-MM-dd");
        var file = kind == ShoppingListKind
            ? $"saturday_shopping_list_email_{datePart}.txt"
            : $"saturday_weekly_stock_email_{datePart}.txt";
        return TreasurerDataPaths.GetConfigFilePath(file);
    }
}
