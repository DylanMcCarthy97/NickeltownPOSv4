using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Services.Migration;

public interface IMigrationBackupService
{
    /// <summary>Copies detected legacy files into a new folder under <paramref name="sourceRoot"/>; originals are never modified.</summary>
    Task<string> CreateBackupSnapshotAsync(string sourceRoot, LegacyJsonDetectionResult detection, Guid runId, CancellationToken cancellationToken = default);
}
