using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Primary + alternate barcode matching shared by Bar and Pitstop sell paths.</summary>
public static class StockAlternateSkuHelper
{
    public static bool AlternateSkuListContains(string? alternateSkusJson, string code)
    {
        foreach (var a in ParseAlternateSkuList(alternateSkusJson))
        {
            if (string.Equals(a, code, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="scanCode"/> matches primary SKU or any alternate,
    /// including leading-zero variants for numeric barcodes.
    /// </summary>
    public static bool MatchesPrimaryOrAlternate(string? primarySku, string? alternateSkusJson, string scanCode)
    {
        foreach (var candidate in ExpandScanCandidates(scanCode))
        {
            if (!string.IsNullOrWhiteSpace(primarySku)
                && string.Equals(primarySku.Trim(), candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (AlternateSkuListContains(alternateSkusJson, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Scan code plus optional leading-zero trim for numeric wedge barcodes.</summary>
    public static IEnumerable<string> ExpandScanCandidates(string code)
    {
        var c = (code ?? string.Empty).Trim();
        if (c.Length == 0)
        {
            yield break;
        }

        yield return c;
        if (c.All(char.IsDigit) && c.Length > 1)
        {
            var trimmed = c.TrimStart('0');
            if (trimmed.Length > 0 && !string.Equals(trimmed, c, StringComparison.Ordinal))
            {
                yield return trimmed;
            }
        }
    }

    public static IEnumerable<string> ParseAlternateSkuList(string? alternateSkusJson) =>
        ParseAlternateSkuListCore(alternateSkusJson);

    public static string? SerializeAlternateSkusFromMultiline(string? text)
    {
        var parts = (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return parts.Count == 0 ? null : JsonSerializer.Serialize(parts);
    }

    private static List<string> ParseAlternateSkuListCore(string? alternateSkusJson)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(alternateSkusJson))
        {
            return result;
        }

        List<string>? list;
        try
        {
            list = JsonSerializer.Deserialize<List<string>>(alternateSkusJson);
        }
        catch (JsonException)
        {
            return result;
        }

        if (list is null)
        {
            return result;
        }

        foreach (var s in list)
        {
            var t = (s ?? string.Empty).Trim();
            if (t.Length > 0)
            {
                result.Add(t);
            }
        }

        return result;
    }
}
