using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace NickeltownPOSV4.Services.Stock;

internal static class StockAlternateSkuHelper
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
