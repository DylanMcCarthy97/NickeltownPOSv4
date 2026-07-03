using System;

using System;

namespace NickeltownPOSV4.Services.AddDrinks;

/// <summary>Identifies the single Shot + Mixer stock-admin item (not shown on the bar product grid).</summary>
public static class ShotMixerCatalog
{
    public const string ItemName = "Shot + Mixer";

    public const string ItemType = "ShotMixer";

    public static bool IsShotMixerItem(string? name, string? itemType) =>
        string.Equals(itemType, ItemType, StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrWhiteSpace(name)
            && name.Contains("shot + mixer", StringComparison.OrdinalIgnoreCase));
}
