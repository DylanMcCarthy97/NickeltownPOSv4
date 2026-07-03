namespace NickeltownPOSV4.Models.Settings;

/// <summary>Where the kiosk looks for update-manifest.json (folder URL, https://, or UNC share).</summary>
public sealed class AppUpdateConfig
{
    /// <summary>Base URL or UNC path, e.g. https://club-server/pos-updates or \\server\pos-updates</summary>
    public string FeedBaseUrl { get; set; } = string.Empty;

    public bool CheckOnStartup { get; set; } = true;

    /// <summary>When true, installs without prompting (kiosk mode). When false, staff confirm first.</summary>
    public bool AutoInstall { get; set; }
}
