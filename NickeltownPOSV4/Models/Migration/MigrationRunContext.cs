using System;

namespace NickeltownPOSV4.Models.Migration;

public sealed class MigrationRunContext
{
    public Guid RunId { get; init; } = Guid.NewGuid();

    public required string SourceRootFolder { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool TreatWarningsAsErrors { get; init; }

    public bool SkipIfAlreadyImported { get; init; } = true;

    public MigrationRunContext WithCompletedUtc(DateTimeOffset completedUtc) => new()
    {
        RunId = RunId,
        SourceRootFolder = SourceRootFolder,
        StartedAtUtc = StartedAtUtc,
        CompletedAtUtc = completedUtc,
        TreatWarningsAsErrors = TreatWarningsAsErrors,
        SkipIfAlreadyImported = SkipIfAlreadyImported,
    };
}
