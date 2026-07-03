using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Stock;

public static class StockItemImageResolver
{
    public static string GetCategoryFallbackEmoji(string? catalogSubCategory)
    {
        var sub = (catalogSubCategory ?? string.Empty).Trim();
        if (sub.Length == 0)
        {
            return "\uD83E\uDD64";
        }

        if (sub.Contains("beer", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("wine", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("cider", StringComparison.OrdinalIgnoreCase))
        {
            return "\uD83C\uDF7A";
        }

        if (sub.Contains("spirit", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("liquor", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("whisk", StringComparison.OrdinalIgnoreCase))
        {
            return "\uD83E\uDD43";
        }

        if (sub.Contains("food", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("snack", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("meal", StringComparison.OrdinalIgnoreCase))
        {
            return "\uD83C\uDF54";
        }

        if (sub.Contains("merch", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("apparel", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("shirt", StringComparison.OrdinalIgnoreCase))
        {
            return "\uD83D\uDC55";
        }

        if (sub.Contains("drink", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("soft", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("soda", StringComparison.OrdinalIgnoreCase)
            || sub.Contains("poptop", StringComparison.OrdinalIgnoreCase))
        {
            return "\uD83E\uDD64";
        }

        return "\uD83E\uDD64";
    }

    private static string DefaultProductImagesDirectory =>
        Path.Combine(AppStoragePaths.GetDocumentsFolder(), AppStoragePaths.RootFolderName, "Images");

    /// <summary>Resolve a loadable file path: managed copy, item path, then legacy JSON image.</summary>
    public static string GetDisplayEmoji(string? imagePath, string? catalogSubCategory) =>
        StockProductIconCatalog.GetDisplayEmoji(imagePath, catalogSubCategory);

    public static string? TryResolve(
        string? imagePath,
        string? rawJson,
        string? catalogSubCategory,
        string? productImagesDirectory = null,
        bool allowBarcodeLookup = false,
        string? sku = null)
    {
        if (StockProductIconCatalog.IsStoragePath(imagePath))
        {
            return null;
        }

        var imagesDir = productImagesDirectory ?? DefaultProductImagesDirectory;
        foreach (var candidate in EnumeratePathCandidates(imagePath, imagesDir))
        {
            var found = TryExistingFile(candidate);
            if (found is not null)
            {
                return found;
            }
        }

        var fromJson = TryExtractImagePathFromRawJson(rawJson);
        if (!string.IsNullOrWhiteSpace(fromJson))
        {
            foreach (var candidate in EnumeratePathCandidates(fromJson, imagesDir))
            {
                var found = TryExistingFile(candidate);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        if (allowBarcodeLookup && !string.IsNullOrWhiteSpace(sku))
        {
            var code = sku.Trim();
            var dir = imagesDir;
            foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" })
            {
                var p = Path.Combine(dir, code + ext);
                if (File.Exists(p))
                {
                    return p;
                }
            }
        }

        _ = catalogSubCategory;
        return null;
    }

    private static IEnumerable<string> EnumeratePathCandidates(string? path, string imagesDir)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        var trimmed = path.Trim();

        if (IsUnderDirectory(trimmed, imagesDir))
        {
            yield return trimmed;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute)
            && absolute.IsFile
            && !string.IsNullOrEmpty(absolute.LocalPath))
        {
            yield return absolute.LocalPath;
        }

        if (Path.IsPathRooted(trimmed))
        {
            yield return Path.GetFullPath(trimmed);
            yield break;
        }

        var fileName = Path.GetFileName(trimmed);
        if (!string.IsNullOrEmpty(fileName))
        {
            yield return Path.Combine(imagesDir, fileName);
        }

        yield return Path.GetFullPath(trimmed);
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        try
        {
            var full = Path.GetFullPath(path.Trim());
            var root = Path.GetFullPath(directory);
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

    private static string? TryExistingFile(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            return File.Exists(full) ? full : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? TryExtractImagePathFromRawJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var n = prop.Name;
                if (!n.Equals("imagepath", StringComparison.OrdinalIgnoreCase)
                    && !n.Equals("image", StringComparison.OrdinalIgnoreCase)
                    && !n.Equals("imageurl", StringComparison.OrdinalIgnoreCase)
                    && !n.Equals("photo", StringComparison.OrdinalIgnoreCase)
                    && !n.Equals("thumbnail", StringComparison.OrdinalIgnoreCase)
                    && !n.Equals("picture", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var s = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return s;
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
