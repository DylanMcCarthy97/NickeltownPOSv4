namespace NickeltownPOSV4.Services;

/// <summary>
/// Permanent POS data locations under Documents\NickeltownPOS\ (outside the MSIX package).
/// </summary>
public interface IAppStoragePaths
{
    string RootFolder { get; }

    string DataFolder { get; }

    string ImagesFolder { get; }

    string ConfigFolder { get; }

    string BackupsFolder { get; }

    string ReportsFolder { get; }

    string ImportsFolder { get; }

    string DatabasePath { get; }

    string SquareConfigPath { get; }

    /// <summary>Migration import logs under <see cref="DataFolder"/>.</summary>
    string MigrationLogsFolder { get; }

    /// <summary>Creates all standard folders if missing.</summary>
    void EnsureDirectories();

    /// <summary>True when running as an installed MSIX package (read-only install dir).</summary>
    bool IsMsixPackaged { get; }

    /// <summary>True if <paramref name="path"/> is under the packaged app install directory.</summary>
    bool IsUnderPackageDirectory(string path);

    /// <summary>Detects writable data files beside the packaged executable (MSIX safety check).</summary>
    bool HasWritableDataBesideExecutable();
}
