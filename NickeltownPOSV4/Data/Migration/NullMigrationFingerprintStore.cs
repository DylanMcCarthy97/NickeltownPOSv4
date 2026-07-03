using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Data.Migration;

/// <summary>Allows repeated imports during V4 bring-up; replace with SQLite-backed store for production deduplication.</summary>
public sealed class NullMigrationFingerprintStore : IMigrationFingerprintStore
{
    public Task<bool> WasSuccessfullyImportedAsync(LegacyJsonFileKind kind, string sourcePath, string contentSha256Hex, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task MarkSuccessfullyImportedAsync(LegacyJsonFileKind kind, string sourcePath, string contentSha256Hex, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
