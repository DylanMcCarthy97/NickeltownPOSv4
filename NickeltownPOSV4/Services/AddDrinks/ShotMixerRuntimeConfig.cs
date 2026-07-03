using System.Collections.Generic;

namespace NickeltownPOSV4.Services.AddDrinks;

public sealed class ShotMixerRuntimeConfig
{
    public long ConfigItemId { get; init; }

    public decimal ShotPrice { get; init; }

    /// <summary>Pre-special shelf price for catalog display when <see cref="ShowSpecialPricing"/> is true.</summary>
    public decimal RegularUnitPrice { get; init; }

    /// <summary>When true, catalog tile shows struck-through regular and red special price.</summary>
    public bool ShowSpecialPricing { get; init; }

    public IReadOnlyList<string> Spirits { get; init; } = new List<string>();
}