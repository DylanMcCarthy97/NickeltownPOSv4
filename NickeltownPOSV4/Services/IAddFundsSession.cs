namespace NickeltownPOSV4.Services;

/// <summary>Target tab for the Add Funds slide panel (set before opening).</summary>
public interface IAddFundsSession
{
    string? TargetTabLegacyId { get; set; }

    string? TargetTabDisplayName { get; set; }

    /// <summary>Balance shown when the panel opens (may refresh after Apply).</summary>
    decimal? TargetTabBalance { get; set; }

    void Clear();
}
