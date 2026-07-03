using System;
using System.IO;

namespace NickeltownPOSV4.Services;

/// <summary>
/// Default <see cref="IReportPathProvider"/>. Resolves paths under Documents\NickeltownPOS\Reports.
/// </summary>
public sealed class LocalReportPathProvider : IReportPathProvider
{
    private const string BarTallyFolderName = "Bar tally reports";
    private const string StockFolderName = "Stock reports";
    private const string PitstopFolderName = "Pitstop reports";
    private const string TabHistoryFolderName = "Tab history exports";

    private readonly IAppStoragePaths _paths;

    public LocalReportPathProvider(IAppStoragePaths paths)
    {
        _paths = paths;
        _paths.EnsureDirectories();
    }

    public string GetRoot() => EnsureDir(_paths.ReportsFolder);

    public string GetBarTallyReportsDirectory() => EnsureDir(Path.Combine(GetRoot(), BarTallyFolderName));

    public string GetStockReportsDirectory() => EnsureDir(Path.Combine(GetRoot(), StockFolderName));

    public string GetPitstopReportsDirectory() => EnsureDir(Path.Combine(GetRoot(), PitstopFolderName));

    public string GetTabHistoryExportsDirectory() => EnsureDir(Path.Combine(GetRoot(), TabHistoryFolderName));

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
