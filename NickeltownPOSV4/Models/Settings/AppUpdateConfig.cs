namespace NickeltownPOSV4.Models.Settings;

/// <summary>Where the kiosk looks for update-manifest.json (folder URL, https://, or UNC share).</summary>
public sealed class AppUpdateConfig
{
    /// <summary>
    /// Preferred public feed for this repo (GitHub rolling release asset folder).
    /// Paste this into Admin → Pit updates if the till feed is empty or stale.
    /// </summary>
    public const string RecommendedFeedBaseUrl =
        "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed";

    /// <summary>Base URL or UNC path, e.g. https://club-server/pos-updates or \\server\pos-updates</summary>
    public string FeedBaseUrl { get; set; } = string.Empty;

    public bool CheckOnStartup { get; set; } = true;

    /// <summary>When true, installs without prompting (kiosk mode). When false, staff confirm first.</summary>
    public bool AutoInstall { get; set; }
}
