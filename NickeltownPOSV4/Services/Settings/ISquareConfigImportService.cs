using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.Settings;

public interface ISquareConfigFilePicker
{
    /// <summary>Opens a file picker for square_config.json. Returns the full path, or null if cancelled.</summary>
    Task<string?> PickSquareConfigFileAsync(string? suggestedFilePath = null);
}

/// <summary>Looks up the POSBarV2 <c>square_config.json</c> file in common locations and imports it into the v4 Settings store.</summary>
public interface ISquareConfigImportService
{
    /// <summary>Returns the resolved path that would be used, or null if no v2 config file is present.</summary>
    string? TryFindLegacyConfigPath();

    /// <summary>Reads and imports the v2 JSON from the first auto-detected path into the v4 Settings store.</summary>
    Task<SquareImportResult> ImportAsync(bool overwriteExisting, CancellationToken cancellationToken = default);

    /// <summary>Reads and imports the v2 JSON from the given file path into the v4 Settings store.</summary>
    Task<SquareImportResult> ImportFromPathAsync(string filePath, bool overwriteExisting, CancellationToken cancellationToken = default);
}

public sealed record SquareImportResult(bool Imported, string? SourcePath, string? Message);
