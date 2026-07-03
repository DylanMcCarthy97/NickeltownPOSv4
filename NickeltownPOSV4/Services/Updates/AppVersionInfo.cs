using System;
using System.Reflection;
using Windows.ApplicationModel;

namespace NickeltownPOSV4.Services.Updates;

public static class AppVersionInfo
{
    private static readonly Lazy<Version> Current = new(ResolveCurrentVersion);

    public static bool IsPackaged => GetIsPackaged();

    public static Version CurrentVersion => Current.Value;

    public static string CurrentVersionString =>
        $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}.{CurrentVersion.Revision}";

    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Trim().Split('.');
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
            {
                return false;
            }
        }

        version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }

    public static bool IsRemoteNewer(string remoteVersion)
    {
        if (!TryParseVersion(remoteVersion, out var remote))
        {
            return false;
        }

        return remote > Current.Value;
    }

    private static bool GetIsPackaged()
    {
        try
        {
            _ = Package.Current.Id.Name;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Version ResolveCurrentVersion()
    {
        if (GetIsPackaged())
        {
            var v = Package.Current.Id.Version;
            return new Version(v.Major, v.Minor, v.Build, v.Revision);
        }

        var asm = Assembly.GetExecutingAssembly().GetName().Version;
        return asm ?? new Version(1, 0, 0, 0);
    }
}
