using System;

namespace NickeltownPOSV4.Services.Updates;

/// <summary>
/// When the till may look for and fit a new build by itself.
/// Never during a signed-in sale — only on the login screen.
/// </summary>
public static class AppUpdateLiveRules
{
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    public static bool ShouldKeepLooking(bool checkOnStartup) => checkOnStartup;

    public static bool CanInstallNow(bool autoInstall, bool signedIn) =>
        autoInstall && !signedIn;
}
