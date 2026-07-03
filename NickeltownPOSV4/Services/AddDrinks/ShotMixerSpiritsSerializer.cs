using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NickeltownPOSV4.Services.Stock;

namespace NickeltownPOSV4.Services.AddDrinks;

internal static class ShotMixerSpiritsSerializer
{
    private static readonly string[] DefaultSpirits =
    [
        "Vodka",
        "Jim Beam",
        "Bundy",
        "Jack Daniels",
        "Gin",
    ];

    public static IReadOnlyList<string> Parse(string? itemDescription)
    {
        var meta = StockItemMetadataSerializer.Parse(itemDescription, isShotMixer: true);
        if (meta.Spirits is { Count: > 0 })
        {
            return NormalizeList(meta.Spirits);
        }

        if (string.IsNullOrWhiteSpace(itemDescription))
        {
            return DefaultSpirits;
        }

        return NormalizeList(
            itemDescription.Split(new[] { '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static string ToStorageJson(IEnumerable<string> spirits)
    {
        var list = NormalizeList(spirits);
        return StockItemMetadataSerializer.ToStorageJson(
            new StockItemMetadataSerializer.StockItemMetadata { Spirits = list.ToList() },
            includeSpirits: true);
    }

    public static string ToDisplayText(IEnumerable<string> spirits) =>
        string.Join(Environment.NewLine, NormalizeList(spirits));

    public static string ToDisplayTextFromDescription(string? itemDescription) =>
        ToDisplayText(Parse(itemDescription));

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string> raw)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var s in raw)
        {
            var t = (s ?? string.Empty).Trim();
            if (t.Length == 0 || !seen.Add(t))
            {
                continue;
            }

            list.Add(t);
        }

        if (list.Count > 0)
        {
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        return DefaultSpirits.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
    }

}