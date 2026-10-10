using System;

namespace NickeltownPOSV4.Services.Tabs;

/// <summary>Bar Tabs–scoped Bathurst visual preference. Does not apply global POS themes.</summary>
public interface ITabsVisualThemeService
{
    /// <summary>User preference persisted for Bathurst on Bar Tabs.</summary>
    bool IsBathurstPreferred { get; }

    /// <summary>True while the Tabs route is visible; Bathurst visuals only apply then.</summary>
    bool IsTabsRouteActive { get; }

    /// <summary>Preferred Bathurst and currently on the Tabs route.</summary>
    bool IsBathurstVisualActive { get; }

    event EventHandler? Changed;

    void SetTabsRouteActive(bool active);

    /// <summary>Load preference from settings (default Normal).</summary>
    System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken = default);

    System.Threading.Tasks.Task SetBathurstPreferredAsync(bool enabled, System.Threading.CancellationToken cancellationToken = default);
}
