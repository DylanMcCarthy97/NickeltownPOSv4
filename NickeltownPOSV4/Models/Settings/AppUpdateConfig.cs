namespace NickeltownPOSV4.Models.Settings;

/// <summary>Update preferences for this till. Feed location is fixed for the product.</summary>
public sealed class AppUpdateConfig
{
    /// <summary>
    /// Built-in public feed (GitHub rolling <c>update-feed</c> release assets).
    /// Tills always check here — no URL to configure in Admin.
    /// </summary>
    public const string BuiltInFeedBaseUrl =
        "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed";

    /// <summary>Obsolete alias kept so older settings JSON still deserializes harmlessly.</summary>
    public const string RecommendedFeedBaseUrl = BuiltInFeedBaseUrl;

    /// <summary>
    /// Legacy field from older builds that stored a custom feed URL.
    /// Ignored at runtime — <see cref="BuiltInFeedBaseUrl"/> is always used.
    /// </summary>
    public string FeedBaseUrl { get; set; } = BuiltInFeedBaseUrl;

    public bool CheckOnStartup { get; set; } = true;

    /// <summary>When true, installs without prompting (kiosk mode). When false, staff confirm first.</summary>
    public bool AutoInstall { get; set; }
}
