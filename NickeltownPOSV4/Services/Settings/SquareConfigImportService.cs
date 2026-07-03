using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NickeltownPOSV4.Services.Settings;

public sealed class SquareConfigImportService : ISquareConfigImportService
{
    private readonly ISquareConfigService _target;

    public SquareConfigImportService(ISquareConfigService target) => _target = target;

    public string? TryFindLegacyConfigPath()
    {
        foreach (var p in BuildCandidatePaths())
        {
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
            {
                return p;
            }
        }

        return null;
    }

    public Task<SquareImportResult> ImportAsync(bool overwriteExisting, CancellationToken cancellationToken = default)
    {
        var path = TryFindLegacyConfigPath();
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(new SquareImportResult(false, null, "No POSBarV2 square_config.json found in any standard location."));
        }

        return ImportFromPathAsync(path, overwriteExisting, cancellationToken);
    }

    public async Task<SquareImportResult> ImportFromPathAsync(
        string filePath,
        bool overwriteExisting,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new SquareImportResult(false, filePath, "Square config file was not found.");
            }

            if (!overwriteExisting)
            {
                var existing = await _target.LoadAsync(cancellationToken).ConfigureAwait(false);
                if (existing is not null
                    && (!string.IsNullOrWhiteSpace(existing.AccessToken)
                        || !string.IsNullOrWhiteSpace(existing.LocationId)
                        || !string.IsNullOrWhiteSpace(existing.DeviceId)))
                {
                    return new SquareImportResult(false, filePath, "Existing Square configuration found. Skipped to avoid overwriting.");
                }
            }

            string json;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream))
            {
                json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }

            var legacy = JsonSerializer.Deserialize<LegacySquareConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (legacy is null)
            {
                return new SquareImportResult(false, filePath, "Square config file was empty or unreadable.");
            }

            var config = new AppSquareConfig
            {
                AccessToken = legacy.AccessToken?.Trim() ?? string.Empty,
                LocationId = legacy.LocationId?.Trim() ?? string.Empty,
                DeviceId = legacy.DeviceId?.Trim() ?? string.Empty,
                Environment = NormalizeEnv(legacy.Environment),
                BarTabCardCatalogVariationId = legacy.BarTabCardCatalogVariationId?.Trim() ?? string.Empty,
                GuestTabCardCatalogVariationId = legacy.GuestTabCardCatalogVariationId?.Trim() ?? string.Empty,
            };

            await _target.SaveAsync(config, cancellationToken).ConfigureAwait(false);

            try
            {
                var dest = Path.Combine(
                    AppStoragePaths.GetDocumentsFolder(),
                    AppStoragePaths.RootFolderName,
                    "Config",
                    "square_config.json");
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(filePath, dest, overwrite: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return new SquareImportResult(true, filePath, $"Imported from {filePath}.");
        }
        catch (Exception ex)
        {
            return new SquareImportResult(false, filePath, $"Import failed: {ex.Message}");
        }
    }

    private static string NormalizeEnv(string? raw)
    {
        var v = (raw ?? string.Empty).Trim();
        return string.Equals(v, "sandbox", StringComparison.OrdinalIgnoreCase) ? "sandbox" : "production";
    }

    private static System.Collections.Generic.IEnumerable<string> BuildCandidatePaths()
    {
        var configPath = Path.Combine(
            AppStoragePaths.GetDocumentsFolder(),
            AppStoragePaths.RootFolderName,
            "Config",
            "square_config.json");
        yield return configPath;

        // Working directory + base directory (legacy portable / debug installs)
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "square_config.json");
        yield return Path.Combine(Environment.CurrentDirectory, "square_config.json");

        // Original v2 build output directories (in case running side-by-side during migration)
        yield return @"D:\POSBAR CLEAN\POSBarV2\square_config.json";
        yield return @"D:\POSBAR CLEAN\POSBarV2\bin\Debug\net8.0-windows\square_config.json";
        yield return @"D:\POSBAR CLEAN\POSBarV2\bin\Release\net8.0-windows\win-x64\square_config.json";
        yield return @"D:\POSBAR CLEAN\POSBarV2\bin\Release\net8.0-windows\win-x64\publish\square_config.json";
        yield return @"D:\POSBAR CLEAN\POSBarV2\finished\square_config.json";

        // %AppData% / %ProgramData% / %LocalAppData% style locations that the v2 DataPathManager often used
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "POSBar", "square_config.json");
            yield return Path.Combine(appData, "NickeltownPOS", "square_config.json");
            yield return Path.Combine(appData, "Nickeltown Flounderers", "square_config.json");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "POSBar", "square_config.json");
            yield return Path.Combine(localAppData, "NickeltownPOS", "square_config.json");
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
        {
            yield return Path.Combine(programData, "POSBar", "square_config.json");
            yield return Path.Combine(programData, "NickeltownPOS", "square_config.json");
        }

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(docs))
        {
            yield return Path.Combine(docs, "POSBar", "square_config.json");
            yield return Path.Combine(docs, "square_config.json");
        }
    }

    private sealed class LegacySquareConfig
    {
        public string? AccessToken { get; set; }

        public string? DeviceId { get; set; }

        public string? Environment { get; set; }

        public string? LocationId { get; set; }

        public string? BarTabCardCatalogVariationId { get; set; }

        public string? GuestTabCardCatalogVariationId { get; set; }
    }
}

public sealed class WinUISquareConfigFilePicker : ISquareConfigFilePicker
{
    private readonly IWindowHandleProvider _window;

    public WinUISquareConfigFilePicker(IWindowHandleProvider window) => _window = window;

    public Task<string?> PickSquareConfigFileAsync(string? suggestedFilePath = null)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };

        picker.FileTypeFilter.Add(".json");

        var hwnd = _window.WindowHandle;
        if (hwnd != 0)
        {
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return WaitForStorageFileAsync(picker.PickSingleFileAsync());
    }

    private static Task<string?> WaitForStorageFileAsync(IAsyncOperation<StorageFile> operation)
    {
        var tcs = new TaskCompletionSource<string?>();

        operation.Completed = (op, status) =>
        {
            switch (status)
            {
                case AsyncStatus.Completed:
                    tcs.TrySetResult(op.GetResults()?.Path);
                    break;
                case AsyncStatus.Canceled:
                    tcs.TrySetResult(null);
                    break;
                case AsyncStatus.Error:
                    tcs.TrySetException(op.ErrorCode as Exception ?? new InvalidOperationException("File picker failed."));
                    break;
                default:
                    tcs.TrySetResult(null);
                    break;
            }
        };

        return tcs.Task;
    }
}
