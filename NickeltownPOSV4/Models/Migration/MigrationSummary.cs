using System;
using System.Collections.Generic;
using System.Linq;

namespace NickeltownPOSV4.Models.Migration;

/// <summary>Human-readable outcome for operators after a migration run.</summary>
public sealed class MigrationSummary
{
    public required Guid RunId { get; init; }

    public required DateTimeOffset StartedAtUtc { get; init; }

    public required DateTimeOffset CompletedAtUtc { get; init; }

    public required string SourceRoot { get; init; }

    public string? BackupFolder { get; init; }

    public string? LogFilePath { get; init; }

    public int TotalImported { get; init; }

    public int TotalSkippedDuplicate { get; init; }

    public int TotalFailedRecords { get; init; }

    public IReadOnlyList<string> Highlights { get; init; } = [];

    public static MigrationSummary FromImport(MigrationImportResult import)
    {
        var segments = import.Segments;
        var totalImported = segments.Sum(s => s.Imported);
        var totalSkipped = segments.Sum(s => s.SkippedDuplicate);
        var totalFailed = segments.Sum(s => s.Failures.Count) + import.GlobalFailures.Count;

        var highlights = new List<string>();
        foreach (var seg in segments)
        {
            if (seg.Imported > 0 || seg.SkippedDuplicate > 0 || seg.Failures.Count > 0)
            {
                highlights.Add($"{seg.Kind}: imported {seg.Imported}, skipped duplicate {seg.SkippedDuplicate}, record failures {seg.Failures.Count}.");
            }
        }

        foreach (var g in import.GlobalFailures)
        {
            highlights.Add($"Global: {g.Message}");
        }

        return new MigrationSummary
        {
            RunId = import.Run.RunId,
            StartedAtUtc = import.Run.StartedAtUtc,
            CompletedAtUtc = import.Run.CompletedAtUtc,
            SourceRoot = import.Run.SourceRootFolder,
            BackupFolder = import.BackupFolder,
            LogFilePath = import.LogFilePath,
            TotalImported = totalImported,
            TotalSkippedDuplicate = totalSkipped,
            TotalFailedRecords = totalFailed,
            Highlights = highlights,
        };
    }
}
