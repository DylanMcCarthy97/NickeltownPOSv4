namespace NickeltownPOSV4.Services.Updates;

public sealed class AppUpdateInstallResult
{
    public bool Ok { get; init; }

    public string? ErrorMessage { get; init; }

    public bool AppShutdownRequested { get; init; }

    public static AppUpdateInstallResult Success(bool shutdown) =>
        new() { Ok = true, AppShutdownRequested = shutdown };

    public static AppUpdateInstallResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}
