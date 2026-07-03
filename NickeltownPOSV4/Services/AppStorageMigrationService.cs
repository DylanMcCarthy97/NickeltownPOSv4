using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services;

public sealed class AppStorageMigrationService : IAppStorageMigrationService
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];

    private readonly IAppStoragePaths _paths;

    public AppStorageMigrationService(IAppStoragePaths paths) => _paths = paths;

    public IReadOnlyList<string> FindLegacyDatabasePaths()
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddExistingFile(found, seen, Path.Combine(AppContext.BaseDirectory, "app.db"), _paths.DatabasePath);
        AddExistingFile(found, seen, Path.Combine(Environment.CurrentDirectory, "app.db"), _paths.DatabasePath);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            AddExistingFile(found, seen, Path.Combine(localAppData, "NickeltownPOSV4", "app.db"), _paths.DatabasePath);
        }
        return found;
    }

    public IReadOnlyList<string> FindLegacySquareConfigPaths()
    {
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddExistingFile(found, seen, Path.Combine(AppContext.BaseDirectory, "square_config.json"), _paths.SquareConfigPath);
        AddExistingFile(found, seen, Path.Combine(Environment.CurrentDirectory, "square_config.json"), _paths.SquareConfigPath);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            foreach (var sub in new[] { "POSBar", "NickeltownPOS", "NickeltownPOSV4" })
            {
                AddExistingFile(found, seen, Path.Combine(localAppData, sub, "square_config.json"), _paths.SquareConfigPath);
            }
        }
        return found;
    }

    public IReadOnlyList<string> FindLegacyProductImageFolders()
    {
        var folders = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            AddFolderWithImages(folders, seen, Path.Combine(localAppData, "NickeltownPOSV4", "ProductImages"));
        }
        AddFolderWithImages(folders, seen, Path.Combine(AppContext.BaseDirectory, "ProductImages"));
        AddFolderWithImages(folders, seen, Path.Combine(Environment.CurrentDirectory, "ProductImages"));
        return folders;
    }

    public Task<bool> CopyDatabaseAsync(string sourcePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => CopyDatabaseCore(sourcePath, cancellationToken), cancellationToken);

    public Task<bool> CopySquareConfigAsync(string sourcePath, CancellationToken cancellationToken = default) =>
        Task.Run(() => CopySquareConfigCore(sourcePath, cancellationToken), cancellationToken);

    public Task<int> CopyProductImagesFromFolderAsync(string sourceFolder, CancellationToken cancellationToken = default) =>
        Task.Run(() => CopyImagesCore(sourceFolder, cancellationToken), cancellationToken);

    private bool CopyDatabaseCore(string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _paths.EnsureDirectories();
        if (!File.Exists(sourcePath)) return false;
        var dest = _paths.DatabasePath;
        if (File.Exists(dest))
        {
            File.Copy(dest, dest + $".pre-migration-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
        }
        File.Copy(sourcePath, dest, overwrite: true);
        return true;
    }

    private bool CopySquareConfigCore(string sourcePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _paths.EnsureDirectories();
        if (!File.Exists(sourcePath)) return false;
        File.Copy(sourcePath, _paths.SquareConfigPath, overwrite: true);
        return true;
    }

    private int CopyImagesCore(string sourceFolder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _paths.EnsureDirectories();
        if (!Directory.Exists(sourceFolder)) return 0;
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(sourceFolder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ImageExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
            File.Copy(file, Path.Combine(_paths.ImagesFolder, Path.GetFileName(file)), overwrite: true);
            count++;
        }
        return count;
    }

    private void AddFolderWithImages(List<string> folders, HashSet<string> seen, string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        try
        {
            var full = Path.GetFullPath(folder);
            if (string.Equals(full, Path.GetFullPath(_paths.ImagesFolder), StringComparison.OrdinalIgnoreCase)) return;
            if (!Directory.Exists(full) || !seen.Add(full)) return;
            if (Directory.EnumerateFiles(full).Any(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)))
            {
                folders.Add(full);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void AddExistingFile(List<string> found, HashSet<string> seen, string? path, string skipPath)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var full = Path.GetFullPath(path);
            if (string.Equals(full, Path.GetFullPath(skipPath), StringComparison.OrdinalIgnoreCase)) return;
            if (File.Exists(full) && seen.Add(full)) found.Add(full);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}