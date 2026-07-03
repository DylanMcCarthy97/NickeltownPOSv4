using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace NickeltownPOSV4.Services;

public sealed class WindowsExportedFileLauncher : IExportedFileLauncher
{
    public bool TryLaunch(string filePath)
    {
        try
        {
            if (!WaitForFile(filePath))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool RevealInExplorer(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    // /select, expects the path quoted; ProcessStartInfo escapes args for us.
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true,
                });
                return true;
            }

            // File missing — fall back to opening the containing folder if it exists.
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                });
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// File.WriteAllBytesAsync flushes before returning, but some AV scanners hold a brief
    /// lock on newly-written files. We poll a few times before giving up to avoid spurious
    /// "file not found" failures from Process.Start.
    /// </summary>
    private static bool WaitForFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (File.Exists(filePath))
            {
                return true;
            }
            Thread.Sleep(50);
        }

        return File.Exists(filePath);
    }
}
