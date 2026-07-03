using System;
using System.Globalization;

namespace NickeltownPOSV4.Services.Tabs;

internal static class TabsBoardActivityFormatter
{
    public static string FormatRelative(string? stamp)
    {
        if (string.IsNullOrWhiteSpace(stamp))
        {
            return "-";
        }

        if (!DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when))
        {
            return "-";
        }

        var local = when.ToLocalTime();
        var ago = DateTimeOffset.Now - local;
        if (ago < TimeSpan.Zero)
        {
            ago = TimeSpan.Zero;
        }

        if (ago.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (ago.TotalMinutes < 60)
        {
            var mins = (int)Math.Floor(ago.TotalMinutes);
            return mins == 1 ? "1 min ago" : $"{mins} min ago";
        }

        if (ago.TotalHours < 24)
        {
            var hrs = (int)Math.Floor(ago.TotalHours);
            return hrs == 1 ? "1 hr ago" : $"{hrs} hr ago";
        }

        if (ago.TotalDays < 7)
        {
            var days = (int)Math.Floor(ago.TotalDays);
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        return local.ToString("d MMM", CultureInfo.CurrentCulture);
    }
}