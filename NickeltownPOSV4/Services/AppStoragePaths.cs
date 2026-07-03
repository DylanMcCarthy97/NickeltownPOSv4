using System;
using System.IO;
using Windows.Storage;

namespace NickeltownPOSV4.Services;

/// <summary>
/// Resolves permanent POS storage under Documents\NickeltownPOS\ for MSIX-safe updates.
/// </summary>
public sealed class AppStoragePaths : IAppStoragePaths
{
    public const string RootFolderName = "NickeltownPOS";

    public AppStoragePaths()
    {
        var documents = GetDocumentsFolder();
        RootFolder = Path.Combine(documents, RootFolderName);
        DataFolder = Path.Combine(RootFolder, "Data");
        ImagesFolder = Path.Combine(RootFolder, "Images");
        ConfigFolder = Path.Combine(RootFolder, "Config");
        BackupsFolder = Path.Combine(RootFolder, "Backups");
        ReportsFolder = Path.Combine(RootFolder, "Reports");
        ImportsFolder = Path.Combine(RootFolder, "Imports");
        MigrationLogsFolder = Path.Combine(DataFolder, "migration-logs");
        DatabasePath = Path.Combine(DataFolder, "app.db");
        SquareConfigPath = Path.Combine(ConfigFolder, "square_config.json");
    }

    public string RootFolder { get; }

    public string DataFolder { get; }

    public string ImagesFolder { get; }

    public string ConfigFolder { get; }

    public string BackupsFolder { get; }

    public string ReportsFolder { get; }

    public string ImportsFolder { get; }

    public string DatabasePath { get; }

    public string SquareConfigPath { get; }

    public string MigrationLogsFolder { get; }

    public bool IsMsixPackaged => TryGetPackageName(out _);

    public void EnsureDirectories()
    {
        foreach (var dir in new[]
                 {
                     RootFolder,
                     DataFolder,
                     ImagesFolder,
                     ConfigFolder,
                     BackupsFolder,
                     ReportsFolder,
                     ImportsFolder,
                     MigrationLogsFolder,
                 })
        {
            Directory.CreateDirectory(dir);
        }
    }

    public bool IsUnderPackageDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(path.Trim());
            var baseDir = Path.GetFullPath(AppContext.BaseDirectory);
            return full.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool HasWritableDataBesideExecutable()
    {
        if (!IsMsixPackaged)
        {
            return false;
        }

        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "app.db"),
            Path.Combine(baseDir, "square_config.json"),
            Path.Combine(baseDir, "credentials.json"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return true;
            }
        }

        return false;
    }

    internal static string GetDocumentsFolder()
    {
        try
        {
            var docs = KnownFolders.DocumentsLibrary.Path;
            if (!string.IsNullOrWhiteSpace(docs))
            {
                return docs;
            }
        }
        catch (Exception)
        {
        }

        var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(myDocs))
        {
            return myDocs;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
    }

    internal static bool TryGetPackageName(out string packageName)
    {
        packageName = string.Empty;
        try
        {
            var id = Windows.ApplicationModel.Package.Current.Id;
            if (!string.IsNullOrWhiteSpace(id?.Name))
            {
                packageName = id.Name;
                return true;
            }
        }
        catch (Exception)
        {
        }

        return false;
    }
}
