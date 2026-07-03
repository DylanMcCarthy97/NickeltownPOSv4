using System.IO;

namespace NickeltownPOSV4.Models.Migration;

/// <summary>Per-file diagnostics for tuning V2 JSON migration against real data.</summary>
public sealed record MigrationFilePreviewDiagnostic
{
    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public required LegacyJsonFileKind FileKind { get; init; }

    /// <summary>JSON value kind of the document root (Array, Object, Null, etc.).</summary>
    public required string RootJsonType { get; init; }

    /// <summary>Where the counted collection was found, e.g. "(root array)", "Items", "Data.Sales".</summary>
    public string? MatchedCollectionPath { get; init; }

    public int RecordCount { get; init; }

    public string? ParseError { get; init; }

    /// <summary>True when this parse failure increments <see cref="MigrationPreviewCounts.UnreadableOrMalformedFiles"/>.</summary>
    public bool CountedAsUnreadableImportFailure { get; init; }

    /// <summary>Tabular file kind, JSON parsed, but zero records extracted (importer path may need tuning).</summary>
    public bool IsZeroCountTabularWarning { get; init; }

    public string FileName => Path.GetFileName(RelativePath);

    public bool HasParseError => ParseError is not null;
}
