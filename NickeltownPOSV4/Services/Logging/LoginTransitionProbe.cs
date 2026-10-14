using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NickeltownPOSV4.Services.Logging;

/// <summary>
/// Temporary Stopwatch instrumentation for the PIN-submit → main POS path.
/// Writes to Debug, the daily POS log, and <c>Documents\NickeltownPOS\logs\login-transition.txt</c>.
/// </summary>
public static class LoginTransitionProbe
{
    private static readonly object Gate = new();
    private static Stopwatch? _watch;
    private static readonly List<string> Lines = new();

    public static string LastSummary { get; private set; } = string.Empty;

    /// <summary>Single-file report to copy after a PIN login. Overwritten each attempt.</summary>
    public static string ReportFilePath =>
        Path.Combine(
            Services.AppStoragePaths.GetDocumentsFolder(),
            Services.AppStoragePaths.RootFolderName,
            "logs",
            "login-transition.txt");

    public static IReadOnlyList<string> LastLines
    {
        get
        {
            lock (Gate)
            {
                return Lines.ToArray();
            }
        }
    }

    public static void Begin(string reason)
    {
        lock (Gate)
        {
            _watch = Stopwatch.StartNew();
            Lines.Clear();
            LastSummary = string.Empty;
            Lines.Add($"BEGIN {reason} t=0.0ms");
        }

        Write($"[LoginTransition] BEGIN {reason} t=0.0ms");
    }

    public static void Mark(string stage)
    {
        double ms;
        string line;
        lock (Gate)
        {
            if (_watch is null)
            {
                return;
            }

            ms = _watch.Elapsed.TotalMilliseconds;
            line = $"{stage} t={ms:0.0}ms";
            Lines.Add(line);
        }

        Write($"[LoginTransition] {line}");
    }

    public static string Complete(string stage)
    {
        Mark(stage);
        string summary;
        lock (Gate)
        {
            var total = _watch?.Elapsed.TotalMilliseconds ?? 0;
            var sb = new StringBuilder();
            sb.Append("LoginTransition total ").Append(total.ToString("0.0")).Append("ms");
            foreach (var line in Lines)
            {
                sb.Append(" | ").Append(line);
            }

            summary = sb.ToString();
            LastSummary = summary;
            _watch?.Stop();
        }

        Write($"[LoginTransition] COMPLETE {summary}");
        return summary;
    }

    private static void Write(string message)
    {
        Debug.WriteLine(message);
        try
        {
            var services = NickeltownPOSV4.App.Services;
            services?.GetService<ILoggerFactory>()
                ?.CreateLogger("LoginTransition")
                ?.LogInformation("{Message}", message);
        }
        catch (Exception)
        {
            // Probe must never affect sign-in.
        }

        try
        {
            if (NickeltownPOSV4.App.Services is null)
            {
                return;
            }

            string snapshot;
            lock (Gate)
            {
                snapshot = string.Join(Environment.NewLine, Lines);
            }

            var path = ReportFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, snapshot + Environment.NewLine + LastSummary + Environment.NewLine);
        }
        catch (Exception)
        {
            // Probe must never affect sign-in.
        }
    }
}
