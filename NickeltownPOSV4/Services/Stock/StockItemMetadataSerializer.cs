using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace NickeltownPOSV4.Services.Stock;

public static class StockItemMetadataSerializer
{
    public static StockItemMetadata Parse(string? itemDescription, bool isShotMixer)
    {
        if (string.IsNullOrWhiteSpace(itemDescription))
            return StockItemMetadata.Empty;

        var trimmed = itemDescription.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            return new StockItemMetadata { Notes = trimmed };

        try
        {
            var doc = JsonSerializer.Deserialize<Payload>(trimmed);
            if (doc is null) return StockItemMetadata.Empty;
            return new StockItemMetadata
            {
                Notes = doc.Notes ?? string.Empty,
                ParLevel = doc.ParLevel,
                SourceSystem = doc.SourceSystem ?? string.Empty,
                MixerItemId = doc.MixerItemId,
                MixerQty = doc.MixerQty is > 0 ? doc.MixerQty.Value : 1,
                Spirits = doc.Spirits,
            };
        }
        catch (JsonException)
        {
            return isShotMixer ? StockItemMetadata.Empty : new StockItemMetadata { Notes = trimmed };
        }
    }

    public static string ToStorageJson(StockItemMetadata meta, bool includeSpirits)
    {
        var payload = new Payload
        {
            Notes = string.IsNullOrWhiteSpace(meta.Notes) ? null : meta.Notes.Trim(),
            ParLevel = meta.ParLevel,
            SourceSystem = string.IsNullOrWhiteSpace(meta.SourceSystem) ? null : meta.SourceSystem.Trim(),
            MixerItemId = meta.MixerItemId,
            MixerQty = meta.MixerItemId is > 0 ? meta.MixerQty : null,
            Spirits = includeSpirits && meta.Spirits is { Count: > 0 } ? meta.Spirits.ToList() : null,
        };
        if (payload.Notes is null && payload.ParLevel is null && payload.SourceSystem is null && payload.MixerItemId is null && payload.Spirits is null)
            return string.Empty;
        return JsonSerializer.Serialize(payload);
    }

    public sealed class StockItemMetadata
    {
        public static StockItemMetadata Empty { get; } = new();
        public string Notes { get; init; } = string.Empty;
        public int? ParLevel { get; init; }
        public string SourceSystem { get; init; } = string.Empty;
        public long? MixerItemId { get; init; }
        public int MixerQty { get; init; } = 1;
        public IReadOnlyList<string>? Spirits { get; init; }
    }

    private sealed class Payload
    {
        public string? Notes { get; set; }
        public int? ParLevel { get; set; }
        public string? SourceSystem { get; set; }
        public long? MixerItemId { get; set; }
        public int? MixerQty { get; set; }
        public List<string>? Spirits { get; set; }
    }
}