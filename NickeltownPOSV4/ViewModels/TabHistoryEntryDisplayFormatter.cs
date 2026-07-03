using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NickeltownPOSV4.Data.Sqlite;

namespace NickeltownPOSV4.ViewModels;

/// <summary>Formats imported V2 ledger rows and V4 SQLite tab entries for the tab history panel.</summary>
internal static class TabHistoryEntryDisplayFormatter
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");

    public static TabHistoryLedgerLine ParseLedgerLine(TabHistoryEntryRow r)
    {
        var whenRaw = string.IsNullOrWhiteSpace(r.OccurredAt) ? r.CreatedAt : r.OccurredAt;
        DateTimeOffset? whenLocal = null;
        if (TryParseWhen(whenRaw, out var parsed))
        {
            whenLocal = parsed;
        }
        else
        {
            var fromJson = ReadTimestampFromJson(r.RawJson);
            if (TryParseWhen(fromJson, out parsed))
            {
                whenLocal = parsed;
                whenRaw = fromJson;
            }
        }

        var whenDisplay = whenLocal?.ToString("dd-MMM-yyyy HH:mm", Culture) ?? FormatWhenForDisplay(whenRaw);
        var monthLabel = whenLocal?.ToString("MMMM yyyy", Culture) ?? "Unknown";

        var type = string.IsNullOrWhiteSpace(r.EntryType) ? "—" : r.EntryType!.Trim();
        var amountDisplay = r.Amount is { } a ? a.ToString("C", Culture) : "—";

        TryReadJsonFields(r.RawJson, out var drinkDetails, out var paymentMethod, out var bartender, out var legacyDetails);

        var details = !string.IsNullOrWhiteSpace(drinkDetails)
            ? drinkDetails!.Trim()
            : !string.IsNullOrWhiteSpace(legacyDetails)
                ? legacyDetails!.Trim()
                : (r.Note ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(details))
        {
            var structured = FormatRawJsonDetail(r.RawJson);
            if (!string.IsNullOrWhiteSpace(structured))
            {
                details = structured;
            }
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            paymentMethod = InferPaymentMethod(type, r.Note);
        }

        if (string.IsNullOrWhiteSpace(bartender))
        {
            bartender = "—";
        }

        return new TabHistoryLedgerLine
        {
            WhenLocal = whenLocal,
            WhenDisplay = whenDisplay,
            MonthGroupLabel = monthLabel,
            Type = type,
            PaymentMethod = paymentMethod,
            AmountDisplay = amountDisplay,
            Bartender = bartender,
            Details = details,
            TypeColorArgb = TypeColorArgb(type),
        };
    }

    public static string FormatLedgerBlock(TabHistoryEntryRow r) => ParseLedgerLine(r).SummaryText;

    /// <summary>Note + structured payload for PDF export (does not repeat when/type/amount).</summary>
    public static string FormatPdfNoteColumn(TabHistoryEntryRow r)
    {
        var n = (r.Note ?? string.Empty).Trim();
        var d = FormatRawJsonDetail(r.RawJson);
        if (string.IsNullOrEmpty(n))
        {
            return string.IsNullOrEmpty(d) ? "—" : d;
        }

        return string.IsNullOrEmpty(d) ? n : $"{n}\n{d}";
    }

    private static string FormatHeaderLine(TabHistoryEntryRow r)
    {
        var whenRaw = string.IsNullOrWhiteSpace(r.OccurredAt) ? r.CreatedAt : r.OccurredAt;
        var when = FormatWhenForDisplay(whenRaw);
        var type = string.IsNullOrWhiteSpace(r.EntryType) ? "—" : r.EntryType!.Trim();
        var amt = r.Amount is { } a ? a.ToString("0.00", CultureInfo.InvariantCulture) : "—";
        var note = (r.Note ?? string.Empty).Trim();
        var core = $"{when}  |  {type}  |  ${amt}";
        return string.IsNullOrEmpty(note) ? core : $"{core}\n    {note}";
    }

    private static readonly Regex MicrosoftJsonDateRegex = new(
        @"^/Date\((-?\d+)(?:[+-]\d{4})?\)/$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string FormatWhenForDisplay(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "—";
        }

        if (TryParseWhen(raw, out var whenLocal))
        {
            return whenLocal.ToString("dd-MMM-yyyy HH:mm", Culture);
        }

        var t = raw.Trim();
        return t.Length > 48 ? t[..48] + "…" : t;
    }

    public static bool TryParseWhen(string? raw, out DateTimeOffset whenLocal)
    {
        whenLocal = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var t = raw.Trim();
        if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
        {
            t = t[1..^1].Trim();
        }

        if (TryParseMicrosoftJsonDate(t, out whenLocal))
        {
            return true;
        }

        var dateIdx = t.IndexOf("/Date(", StringComparison.Ordinal);
        if (dateIdx >= 0)
        {
            var endIdx = t.IndexOf(")/", dateIdx, StringComparison.Ordinal);
            if (endIdx > dateIdx)
            {
                var slice = t[dateIdx..(endIdx + 2)];
                if (TryParseMicrosoftJsonDate(slice, out whenLocal))
                {
                    return true;
                }
            }
        }

        if (DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            whenLocal = dto.ToLocalTime();
            return true;
        }

        return false;
    }

    private static bool TryParseMicrosoftJsonDate(string t, out DateTimeOffset whenLocal)
    {
        whenLocal = default;
        var msMatch = MicrosoftJsonDateRegex.Match(t.Trim());
        if (!msMatch.Success
            || !long.TryParse(msMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
        {
            return false;
        }

        whenLocal = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
        return true;
    }

    private static string? ReadTimestampFromJson(string? rawJson)
    {
        var raw = (rawJson ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return ReadString(root, "Timestamp", "timestamp", "OccurredAt", "occurredAt", "CreatedAt", "createdAt");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string TypeColorArgb(string type)
    {
        if (type.Equals("Reimbursement", StringComparison.OrdinalIgnoreCase))
        {
            return "#FF5F9EA0";
        }

        if (type.Equals("Payment", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Square", StringComparison.OrdinalIgnoreCase)
            || type.Contains("top-up", StringComparison.OrdinalIgnoreCase))
        {
            return "#FF006400";
        }

        if (type.Contains("Raffle", StringComparison.OrdinalIgnoreCase))
        {
            return "#FFFF8C00";
        }

        return "#FF111827";
    }

    private static string InferPaymentMethod(string type, string? note)
    {
        if (type.Contains("Square", StringComparison.OrdinalIgnoreCase))
        {
            return "Square";
        }

        if (type.Contains("Cash", StringComparison.OrdinalIgnoreCase))
        {
            return "Cash";
        }

        if (type.Equals("Payment", StringComparison.OrdinalIgnoreCase))
        {
            var n = (note ?? string.Empty).Trim();
            if (n.Length > 0 && n.Length <= 24)
            {
                return n;
            }

            return "Payment";
        }

        return "—";
    }

    private static void TryReadJsonFields(
        string? rawJson,
        out string? drinkDetails,
        out string? paymentMethod,
        out string? bartender,
        out string? legacyDetails)
    {
        drinkDetails = null;
        paymentMethod = null;
        bartender = null;
        legacyDetails = null;

        var raw = (rawJson ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            drinkDetails = ReadString(root, "DrinkDetails", "drinkDetails");
            paymentMethod = ReadString(root, "PaymentMethod", "paymentMethod", "Method", "method");
            bartender = ReadString(root, "BartenderInitials", "bartenderInitials", "Bartender", "bartender");
            legacyDetails = ReadString(root, "Details", "details");

            if (string.IsNullOrWhiteSpace(drinkDetails))
            {
                drinkDetails = TryFormatV4DrinkDetail(root);
            }
        }
        catch (JsonException)
        {
            // ignore malformed payloads
        }
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetPropertyIgnoreCase(root, name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString()?.Trim();
                if (!string.IsNullOrEmpty(s))
                {
                    return s;
                }
            }
        }

        return null;
    }

    private static string? FormatRawJsonDetail(string? rawJson)
    {
        var raw = (rawJson ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var drink = TryFormatV4DrinkDetail(root);
            if (!string.IsNullOrEmpty(drink))
            {
                return drink;
            }

            return TryFormatLegacyExtensionDetail(root);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryFormatV4DrinkDetail(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, "displayName", out var nameEl)
            || nameEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var name = nameEl.GetString()?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var parts = new List<string> { name };

        if (TryGetPropertyIgnoreCase(root, "quantity", out var qtyEl) && qtyEl.TryGetInt32(out var qty) && qty > 0)
        {
            parts.Add($"qty {qty.ToString(CultureInfo.InvariantCulture)}");
        }

        if (TryGetPropertyIgnoreCase(root, "unitPrice", out var upEl) && TryGetDecimal(upEl, out var up))
        {
            parts.Add($"@ ${up.ToString("0.00", CultureInfo.InvariantCulture)}");
        }

        if (TryGetPropertyIgnoreCase(root, "lineTotal", out var ltEl) && TryGetDecimal(ltEl, out var lt))
        {
            parts.Add($"line ${lt.ToString("0.00", CultureInfo.InvariantCulture)}");
        }

        if (TryGetPropertyIgnoreCase(root, "itemId", out var idEl) && idEl.TryGetInt64(out var itemId) && itemId > 0)
        {
            parts.Add($"item #{itemId.ToString(CultureInfo.InvariantCulture)}");
        }

        if (TryGetPropertyIgnoreCase(root, "drinkCommitBatchId", out var bEl)
            && bEl.ValueKind == JsonValueKind.String)
        {
            var b = bEl.GetString();
            if (!string.IsNullOrEmpty(b) && b.Length > 10)
            {
                parts.Add($"batch {b[..8]}…");
            }
        }

        return string.Join(" · ", parts);
    }

    private static string? TryFormatLegacyExtensionDetail(JsonElement root)
    {
        var extras = new List<string>();
        foreach (var p in root.EnumerateObject())
        {
            if (NameEq(p.Name, "Type")
                || NameEq(p.Name, "Amount")
                || NameEq(p.Name, "Note")
                || NameEq(p.Name, "Timestamp")
                || NameEq(p.Name, "Id"))
            {
                continue;
            }

            var text = JsonElementToShortText(p.Value);
            if (!string.IsNullOrWhiteSpace(text))
            {
                extras.Add($"{p.Name}: {text}");
            }
        }

        return extras.Count == 0 ? null : string.Join(" · ", extras);
    }

    private static bool NameEq(string propertyName, string expected) =>
        propertyName.Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement value)
    {
        foreach (var p in root.EnumerateObject())
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetDecimal(JsonElement el, out decimal d)
    {
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out d))
        {
            return true;
        }

        if (el.ValueKind == JsonValueKind.String
            && decimal.TryParse(el.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out d))
        {
            return true;
        }

        d = 0;
        return false;
    }

    private static string JsonElementToShortText(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
            {
                var s = el.GetString()?.Trim() ?? string.Empty;
                return s.Length > 120 ? s[..120] + "…" : s;
            }

            case JsonValueKind.Number:
                return el.GetRawText();

            case JsonValueKind.True:
                return "true";

            case JsonValueKind.False:
                return "false";

            case JsonValueKind.Object:
            case JsonValueKind.Array:
            {
                var s = el.GetRawText();
                return s.Length > 96 ? s[..96] + "…" : s;
            }

            default:
                return string.Empty;
        }
    }
}
