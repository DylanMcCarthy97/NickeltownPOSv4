namespace NickeltownPOSV4.Services;

/// <summary>
/// Shared tab identity for slide panels (add drinks, add funds, edit tab, history).
/// Only one panel flow is active at a time; balance is a display snapshot when the panel opens.
/// </summary>
public interface ITabPanelTargetState
{
    string? TabLegacyId { get; set; }

    string? TabDisplayName { get; set; }

    /// <summary>Balance snapshot when a panel opened (refresh after commits via workspace bus).</summary>
    decimal? DisplayBalance { get; set; }

    bool IsGuestTab { get; set; }

    void ClearIdentity();

    void ClearAll();
}
