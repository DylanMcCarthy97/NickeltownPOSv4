using System;
using System.IO;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Stock;

/// <summary>Copies product images into Documents\NickeltownPOS\Images.</summary>
public sealed class StockProductImageStorage : IStockProductImageStorage
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];

    private readonly IAppStoragePaths _paths;

    public StockProductImageStorage(IAppStoragePaths paths)
    {
        _paths = paths;
        _paths.EnsureDirectories();
    }

    public string ProductImagesDirectory => _paths.ImagesFolder;

    public bool IsManagedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(path.Trim());
            var root = Path.GetFullPath(ProductImagesDirectory);
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
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

    public ProductImageImportResult ImportFromSource(string sourcePath, long? itemId)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return ProductImageImportResult.Fail("No image file selected.");
        }

        try
        {
            var sourceFull = Path.GetFullPath(sourcePath.Trim());
            if (!File.Exists(sourceFull))
            {
                return ProductImageImportResult.Fail("Selected image file was not found.");
            }

            if (_paths.IsUnderPackageDirectory(sourceFull))
            {
                return ProductImageImportResult.Fail(
                    "Cannot store product images beside the app install folder. Images are saved under Documents\\NickeltownPOS\\Images.");
            }

            var ext = NormalizeExtension(sourceFull);
            if (ext is null)
            {
                return ProductImageImportResult.Fail("Unsupported image type. Use JPG, PNG, WEBP, GIF, or BMP.");
            }

            var dir = ProductImagesDirectory;
            Directory.CreateDirectory(dir);
            var fileName = itemId is > 0 ? $"{itemId}{ext}" : $"{Guid.NewGuid():N}{ext}";
            var dest = Path.Combine(dir, fileName);
            File.Copy(sourceFull, dest, overwrite: true);
            return ProductImageImportResult.Ok(dest);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ProductImageImportResult.Fail($"Could not save image: {ex.Message}");
        }
        catch (IOException ex)
        {
            return ProductImageImportResult.Fail($"Could not save image: {ex.Message}");
        }
    }

    public string? EnsureItemIdFileName(string? storedPath, long itemId)
    {
        if (itemId <= 0 || string.IsNullOrWhiteSpace(storedPath))
        {
            return storedPath;
        }

        try
        {
            var current = Path.GetFullPath(storedPath.Trim());
            if (!File.Exists(current))
            {
                return storedPath;
            }

            var ext = Path.GetExtension(current);
            if (string.IsNullOrEmpty(ext))
            {
                ext = ".jpg";
            }

            var dest = Path.Combine(ProductImagesDirectory, $"{itemId}{ext}");
            if (string.Equals(current, Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
            {
                return dest;
            }

            Directory.CreateDirectory(ProductImagesDirectory);
            File.Copy(current, dest, overwrite: true);
            if (!string.Equals(current, dest, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(current);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return dest;
        }
        catch (IOException)
        {
            return storedPath;
        }
        catch (UnauthorizedAccessException)
        {
            return storedPath;
        }
    }

    public string? ResolveImportImagePath(string? imagePath, long itemId)
    {
        if (itemId <= 0 || string.IsNullOrWhiteSpace(imagePath))
        {
            return string.IsNullOrWhiteSpace(imagePath) ? null : imagePath.Trim();
        }

        var trimmed = imagePath.Trim();
        if (StockProductIconCatalog.IsStoragePath(trimmed))
        {
            return trimmed;
        }

        if (IsManagedPath(trimmed))
        {
            return EnsureItemIdFileName(trimmed, itemId) ?? trimmed;
        }

        var source = StockItemImageResolver.TryResolve(trimmed, null, null, ProductImagesDirectory) ?? trimmed;
        try
        {
            if (!File.Exists(source))
            {
                return trimmed;
            }
        }
        catch (IOException)
        {
            return trimmed;
        }
        catch (UnauthorizedAccessException)
        {
            return trimmed;
        }

        var copy = ImportFromSource(source, itemId);
        return copy.Success ? copy.StoredPath : trimmed;
    }

    private static string? NormalizeExtension(string path)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
        {
            return ".jpg";
        }

        ext = ext.ToLowerInvariant();
        foreach (var allowed in AllowedExtensions)
        {
            if (ext == allowed)
            {
                return ext;
            }
        }

        return null;
    }
}

public sealed class ProductImageImportResult
{
    public bool Success { get; init; }

    public string? StoredPath { get; init; }

    public string? ErrorMessage { get; init; }

    public static ProductImageImportResult Ok(string path) =>
        new() { Success = true, StoredPath = path };

    public static ProductImageImportResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
