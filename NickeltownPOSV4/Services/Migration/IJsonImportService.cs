using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Migration;

namespace NickeltownPOSV4.Services.Migration;

public interface IJsonImportService
{
    Task<LegacyJsonDetectionResult> DetectAsync(string sourceRootFolder, CancellationToken cancellationToken = default);

    Task<MigrationPreviewBuildResult> BuildPreviewAsync(LegacyJsonDetectionResult detection, CancellationToken cancellationToken = default);

    Task<MigrationValidationResult> ValidateAsync(LegacyJsonDetectionResult detection, CancellationToken cancellationToken = default);

    Task<string> CreateBackupAsync(LegacyJsonDetectionResult detection, Guid runId, CancellationToken cancellationToken = default);

    Task<MigrationImportResult> ImportAsync(
        LegacyJsonDetectionResult detection,
        MigrationRunContext context,
        string? backupFolder,
        CancellationToken cancellationToken = default);
}
