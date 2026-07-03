using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Services.Migration;

public sealed class MigrationBackupService : IMigrationBackupService
{
    public async Task<string> CreateBackupSnapshotAsync(string sourceRoot, LegacyJsonDetectionResult detection, Guid runId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            throw new ArgumentException("Source root is required.", nameof(sourceRoot));
        }

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupRoot = Path.Combine(sourceRoot, "NickeltownPOSV4_MigrationBackups", $"{stamp}_{runId:N}");
        Directory.CreateDirectory(backupRoot);

        foreach (var file in detection.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relative = file.RelativePath;
            var destPath = Path.Combine(backupRoot, relative);
            var destDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            await CopyFileNoOverwriteAsync(file.FullPath, destPath, cancellationToken).ConfigureAwait(false);
        }

        return backupRoot;
    }

    private static async Task CopyFileNoOverwriteAsync(string sourcePath, string destPath, CancellationToken cancellationToken)
    {
        if (File.Exists(destPath))
        {
            throw new IOException($"Backup destination already exists (unexpected): {destPath}");
        }

        const int bufferSize = 1024 * 1024;
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var dest = new FileStream(destPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(dest, cancellationToken).ConfigureAwait(false);
    }
}
