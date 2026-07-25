namespace NickeltownPOSV4.Services.Updates;

/// <summary>Pit-stop stages reported while an update is applied.</summary>
public enum AppUpdateStage
{
    /// <summary>Downloading the MSIX package.</summary>
    PullIn,

    /// <summary>Installing / registering the new package.</summary>
    FitBuild,

    /// <summary>Arming relaunch and restarting.</summary>
    Restart,
}

/// <summary>
/// Progress for an update install. Percent is 0-100 when the source reports a
/// content length, otherwise null (indeterminate).
/// </summary>
public sealed record AppUpdateProgress(AppUpdateStage Stage, string Message, double? Percent = null);
