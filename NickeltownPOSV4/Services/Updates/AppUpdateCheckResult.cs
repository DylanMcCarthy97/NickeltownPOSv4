namespace NickeltownPOSV4.Services.Updates;

public sealed class AppUpdateCheckResult
{
    public bool Checked { get; init; }

    public bool UpdateAvailable { get; init; }

    public string? ErrorMessage { get; init; }

    public AppUpdateManifest? Manifest { get; init; }

    public static AppUpdateCheckResult Skipped(string? reason = null) =>
        new() { Checked = false, ErrorMessage = reason };

    public static AppUpdateCheckResult None() => new() { Checked = true };

    public static AppUpdateCheckResult Available(AppUpdateManifest manifest) =>
        new() { Checked = true, UpdateAvailable = true, Manifest = manifest };

    public static AppUpdateCheckResult Failed(string message) =>
        new() { Checked = true, ErrorMessage = message };
}
