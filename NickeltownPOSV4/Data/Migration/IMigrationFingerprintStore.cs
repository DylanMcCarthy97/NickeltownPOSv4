using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Data.Migration;

/// <summary>
/// Tracks successful legacy imports so re-runs can skip duplicates. Production implementation will persist in SQLite.
/// </summary>
public interface IMigrationFingerprintStore
{
    Task<bool> WasSuccessfullyImportedAsync(LegacyJsonFileKind kind, string sourcePath, string contentSha256Hex, CancellationToken cancellationToken = default);

    Task MarkSuccessfullyImportedAsync(LegacyJsonFileKind kind, string sourcePath, string contentSha256Hex, CancellationToken cancellationToken = default);
}
