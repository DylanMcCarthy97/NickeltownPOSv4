namespace NickeltownPOSV4.Models.Settings;

/// <summary>Square Terminal API credentials and identifiers (no SDK calls; persisted for downstream services).</summary>
public sealed class AppSquareConfig
{
    public string AccessToken { get; set; } = string.Empty;

    public string LocationId { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    /// <summary>"sandbox" or "production".</summary>
    public string Environment { get; set; } = "production";

    /// <summary>Catalog variation ID for bar tab card top-ups (optional).</summary>
    public string BarTabCardCatalogVariationId { get; set; } = string.Empty;

    /// <summary>Catalog variation ID for guest tab card settlements (optional).</summary>
    public string GuestTabCardCatalogVariationId { get; set; } = string.Empty;

    /// <summary>Pass-through percent added to Pitstop Terminal card totals before sending to Square (e.g. 1.7 = 1.7%).</summary>
    public decimal PitstopTerminalCardSurchargePercent { get; set; } = 1.7m;
}
