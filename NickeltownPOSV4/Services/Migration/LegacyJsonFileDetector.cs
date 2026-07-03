using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Services.Migration;

public sealed class LegacyJsonFileDetector : ILegacyJsonFileDetector
{
    private static readonly (string FileName, LegacyJsonFileKind Kind)[] RootFileMap =
    [
        ("categories.json", LegacyJsonFileKind.Categories),
        ("unified_categories.json", LegacyJsonFileKind.Categories),
        ("drinks.json", LegacyJsonFileKind.Drinks),
        ("items.json", LegacyJsonFileKind.Items),
        ("tabs.json", LegacyJsonFileKind.Tabs),
        ("members.json", LegacyJsonFileKind.Members),
        ("bartenders.json", LegacyJsonFileKind.Bartenders),
        ("PitstopSalesData.json", LegacyJsonFileKind.PitstopSalesData),
        ("square_config.json", LegacyJsonFileKind.SquareConfig),
    ];

    private static readonly string[] SettingsFolderHints = ["settings", "config", "configuration"];

    /// <summary>Known V2 stock/product JSON filenames (any subfolder). Classified as <see cref="LegacyJsonFileKind.Items"/>.</summary>
    private static readonly HashSet<string> CategoryLikeFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "categories.json",
        "unified_categories.json",
        "categorylist.json",
        "itemcategories.json",
        "productcategories.json",
        "departments.json",
        "menu_categories.json",
    };

    private static readonly HashSet<string> StockLikeItemsFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "stockitems.json",
        "stock_items.json",
        "stock items.json",
        "inventory.json",
        "products.json",
        "productitems.json",
        "baritems.json",
        "menuitems.json",
        "catalog.json",
        "menu.json",
    };

    /// <summary>Alternate tab exports (any subfolder). Classified as <see cref="LegacyJsonFileKind.Tabs"/>.</summary>
    private static readonly HashSet<string> TabLikeFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tabs.json",
        "open_tabs.json",
        "opentabs.json",
        "active_tabs.json",
        "activetabs.json",
        "tabs_export.json",
        "tabexport.json",
        "exported_tabs.json",
    };

    /// <summary>Pitstop archives beside the canonical root file (any subfolder).</summary>
    private static readonly HashSet<string> PitstopLikeFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pitstopsalesdata.json",
        "pitstop_sales.json",
        "pitstopsales.json",
        "PitstopSales.json",
    };

    public Task<LegacyJsonDetectionResult> DetectAsync(string rootFolder, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
        {
            return Task.FromResult(new LegacyJsonDetectionResult { RootFolder = rootFolder, Files = [] });
        }

        var list = new List<LegacyDetectedFile>();

        foreach (var (fileName, kind) in RootFileMap)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = Directory.EnumerateFiles(rootFolder, fileName, SearchOption.TopDirectoryOnly)
                .FirstOrDefault(p => string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                AddFile(list, rootFolder, match, kind);
            }
        }

        foreach (var path in Directory.EnumerateFiles(rootFolder, "*.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fn = Path.GetFileName(path);
            if (CategoryLikeFileNames.Contains(fn))
            {
                AddFile(list, rootFolder, path, LegacyJsonFileKind.Categories);
                continue;
            }

            if (StockLikeItemsFileNames.Contains(fn))
            {
                AddFile(list, rootFolder, path, LegacyJsonFileKind.Items);
                continue;
            }

            if (TabLikeFileNames.Contains(fn))
            {
                AddFile(list, rootFolder, path, LegacyJsonFileKind.Tabs);
                continue;
            }

            if (PitstopLikeFileNames.Contains(fn))
            {
                AddFile(list, rootFolder, path, LegacyJsonFileKind.PitstopSalesData);
            }
        }

        foreach (var hint in SettingsFolderHints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sub = Path.Combine(rootFolder, hint);
            if (!Directory.Exists(sub))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(sub, "*.json", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var settingsFn = Path.GetFileName(path);
                if (StockLikeItemsFileNames.Contains(settingsFn)
                    || CategoryLikeFileNames.Contains(settingsFn)
                    || TabLikeFileNames.Contains(settingsFn)
                    || PitstopLikeFileNames.Contains(settingsFn))
                {
                    continue;
                }

                AddFile(list, rootFolder, path, LegacyJsonFileKind.SettingsOrConfig);
            }
        }

        foreach (var loose in new[] { "config.json", "appsettings.json", "settings.json" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = Directory.EnumerateFiles(rootFolder, loose, SearchOption.TopDirectoryOnly)
                .FirstOrDefault(p => string.Equals(Path.GetFileName(p), loose, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                AddFile(list, rootFolder, match, LegacyJsonFileKind.SettingsOrConfig);
            }
        }

        list.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(new LegacyJsonDetectionResult
        {
            RootFolder = rootFolder,
            Files = list,
        });
    }

    private static void AddFile(List<LegacyDetectedFile> list, string root, string fullPath, LegacyJsonFileKind kind)
    {
        var rel = Path.GetRelativePath(root, fullPath);
        if (list.Any(f => string.Equals(f.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        long len = 0;
        try
        {
            len = new FileInfo(fullPath).Length;
        }
        catch
        {
            // best-effort
        }

        list.Add(new LegacyDetectedFile
        {
            Kind = kind,
            FullPath = fullPath,
            RelativePath = rel,
            LengthBytes = len,
        });
    }
}
