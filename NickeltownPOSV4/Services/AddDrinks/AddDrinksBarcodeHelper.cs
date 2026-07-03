using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NickeltownPOSV4.ViewModels;

namespace NickeltownPOSV4.Services.AddDrinks;

/// <summary>SKU / alternate barcode lookup for wedge scanners.</summary>
internal static class AddDrinksBarcodeHelper
{
    public static DrinkCardItem? FindProduct(IReadOnlyList<DrinkCardItem> products, string code)
    {
        foreach (var candidate in ExpandScanCandidates(code))
        {
            var hit = products.FirstOrDefault(p =>
                (!string.IsNullOrWhiteSpace(p.Sku)
                    && string.Equals(p.Sku.Trim(), candidate, StringComparison.OrdinalIgnoreCase))
                || p.AlternateSkus.Any(a =>
                    string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase)));
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

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

    public static IReadOnlyList<string> ParseAlternateSkusJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list is null)
            {
                return Array.Empty<string>();
            }

            return list
                .Select(s => (s ?? string.Empty).Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
