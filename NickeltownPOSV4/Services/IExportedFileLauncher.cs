namespace NickeltownPOSV4.Services;

/// <summary>Opens a generated report file with the OS default handler (e.g. PDF viewer).</summary>
public interface IExportedFileLauncher
{
    /// <summary>
    /// Try to open <paramref name="filePath"/> with the user's default application.
    /// Returns false if the file is missing or the OS could not start the handler — the
    /// caller can then fall back to <see cref="RevealInExplorer"/> so the user still gets
    /// a way to find the saved file.
    /// </summary>
    bool TryLaunch(string filePath);

    /// <summary>
    /// Open File Explorer with <paramref name="filePath"/> highlighted. Returns false if
    /// the path is missing or Explorer could not be started.
    /// </summary>
    bool RevealInExplorer(string filePath);
}
