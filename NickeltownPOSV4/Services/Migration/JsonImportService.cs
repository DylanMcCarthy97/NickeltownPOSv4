using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Migration;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Migration;
using NickeltownPOSV4.Models.Migration.LegacyJsonModels;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services.Migration;

public sealed class JsonImportService : IJsonImportService
{
    private readonly ILegacyJsonFileDetector _detector;
    private readonly IMigrationBackupService _backup;
    private readonly IBackupService _dbBackup;
    private readonly IMigrationFingerprintStore _fingerprints;
    private readonly ITabMigrationRepository _tabs;
    private readonly IMemberMigrationRepository _members;
    private readonly IItemMigrationRepository _items;
    private readonly IDrinkMigrationRepository _drinks;
    private readonly IBartenderMigrationRepository _bartenders;
    private readonly IPitstopSalesMigrationRepository _pitstopSales;
    private readonly ISquareConfigMigrationRepository _square;
    private readonly IAppSettingsMigrationRepository _settings;

    private readonly ICategoryMigrationRepository _categories;

    private readonly IMigrationRunJournal _migrationRunJournal;

    private readonly IStaffPinLookupCache _pinCache;

    private readonly IAppStoragePaths _paths;

    public JsonImportService(
        ILegacyJsonFileDetector detector,
        IMigrationBackupService backup,
        IBackupService dbBackup,
        IMigrationFingerprintStore fingerprints,
        ITabMigrationRepository tabs,
        IMemberMigrationRepository members,
        IItemMigrationRepository items,
        IDrinkMigrationRepository drinks,
        IBartenderMigrationRepository bartenders,
        IPitstopSalesMigrationRepository pitstopSales,
        ISquareConfigMigrationRepository square,
        IAppSettingsMigrationRepository settings,
        ICategoryMigrationRepository categories,
        IMigrationRunJournal migrationRunJournal,
        IStaffPinLookupCache pinCache,
        IAppStoragePaths paths)
    {
        _detector = detector;
        _backup = backup;
        _dbBackup = dbBackup;
        _fingerprints = fingerprints;
        _tabs = tabs;
        _members = members;
        _items = items;
        _drinks = drinks;
        _bartenders = bartenders;
        _pitstopSales = pitstopSales;
        _square = square;
        _settings = settings;
        _categories = categories;
        _migrationRunJournal = migrationRunJournal;
        _pinCache = pinCache;
        _paths = paths;
    }

    /// <summary>Import order: categories and members before tabular inventory so FK/name resolution succeeds.</summary>
    private static int ImportKindOrdinal(LegacyJsonFileKind kind) => kind switch
    {
        LegacyJsonFileKind.Categories => 0,
        LegacyJsonFileKind.Members => 10,
        LegacyJsonFileKind.Bartenders => 15,
        LegacyJsonFileKind.Tabs => 20,
        LegacyJsonFileKind.Items => 30,
        LegacyJsonFileKind.Drinks => 35,
        LegacyJsonFileKind.PitstopSalesData => 40,
        LegacyJsonFileKind.SquareConfig => 90,
        LegacyJsonFileKind.SettingsOrConfig => 100,
        _ => 200,
    };

    public Task<LegacyJsonDetectionResult> DetectAsync(string sourceRootFolder, CancellationToken cancellationToken = default)
        => _detector.DetectAsync(sourceRootFolder, cancellationToken);

    public async Task<MigrationPreviewBuildResult> BuildPreviewAsync(LegacyJsonDetectionResult detection, CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<MigrationFilePreviewDiagnostic>();
        var counts = new MigrationPreviewCounts();
        var unreadable = 0;

        foreach (var file in detection.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (doc, error) = await LegacyJsonDocumentReader.TryLoadDocumentAsync(file.FullPath, cancellationToken).ConfigureAwait(false);
            if (doc is null || error is not null)
            {
                var countAsUnreadable = IsTabularMigrationKind(file.Kind);
                if (countAsUnreadable)
                {
                    unreadable++;
                }

                diagnostics.Add(new MigrationFilePreviewDiagnostic
                {
                    FullPath = file.FullPath,
                    RelativePath = file.RelativePath,
                    FileKind = file.Kind,
                    RootJsonType = "ParseError",
                    MatchedCollectionPath = null,
                    RecordCount = 0,
                    ParseError = error ?? "Unknown parse error.",
                    CountedAsUnreadableImportFailure = countAsUnreadable,
                    IsZeroCountTabularWarning = false,
                });
                continue;
            }

            using (doc)
            {
                diagnostics.Add(AnalyzeParsedFileForPreview(file, doc.RootElement, ref counts));
            }
        }

        diagnostics.Sort(ComparePreviewDiagnostics);

        return new MigrationPreviewBuildResult
        {
            Counts = counts with { UnreadableOrMalformedFiles = unreadable },
            Diagnostics = diagnostics,
        };
    }

    private static int ComparePreviewDiagnostics(MigrationFilePreviewDiagnostic a, MigrationFilePreviewDiagnostic b)
    {
        static int Tier(MigrationFilePreviewDiagnostic d)
        {
            // Tabular parse failures (unreadable) sort first.
            if (d.CountedAsUnreadableImportFailure)
            {
                return 0;
            }

            // Settings/config parse issues are warnings, not top-tier errors.
            if (d.HasParseError)
            {
                return 1;
            }

            if (d.IsZeroCountTabularWarning)
            {
                return 2;
            }

            return 3;
        }

        var t = Tier(a).CompareTo(Tier(b));
        if (t != 0)
        {
            return t;
        }

        return string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTabularMigrationKind(LegacyJsonFileKind kind) =>
        kind is LegacyJsonFileKind.Drinks or LegacyJsonFileKind.Items or LegacyJsonFileKind.Categories or LegacyJsonFileKind.Tabs
            or LegacyJsonFileKind.Members or LegacyJsonFileKind.Bartenders or LegacyJsonFileKind.PitstopSalesData;

    private static MigrationFilePreviewDiagnostic AnalyzeParsedFileForPreview(
        LegacyDetectedFile file,
        JsonElement root,
        ref MigrationPreviewCounts counts)
    {
        var rootType = LegacyJsonPayloadExtractor.DescribeJsonValueKind(root.ValueKind);
        string? match = null;
        var recordCount = 0;

        switch (file.Kind)
        {
            case LegacyJsonFileKind.SettingsOrConfig:
                counts = counts with { SettingsDocuments = counts.SettingsDocuments + 1 };
                recordCount = 1;
                match = "(settings/config document)";
                break;

            case LegacyJsonFileKind.SquareConfig:
                counts = counts with { SquareConfigRoots = counts.SquareConfigRoots + 1 };
                recordCount = 1;
                match = "(square integration document)";
                break;

            case LegacyJsonFileKind.Tabs:
                if (LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(root, LegacyJsonFileKind.Tabs, out var tabsArray, out match))
                {
                    recordCount = LegacyJsonPayloadExtractor.CountArrayElements(tabsArray);
                    var hist = LegacyJsonPayloadExtractor.CountTabHistoryLinesInTabsArray(tabsArray);
                    counts = counts with { Tabs = counts.Tabs + recordCount, TabHistoryEntries = counts.TabHistoryEntries + hist };
                }
                else if (LegacyJsonPayloadExtractor.TryRecognizeSingleTabObject(root, out var historyLines))
                {
                    recordCount = 1;
                    match = "(single tab object)";
                    counts = counts with { Tabs = counts.Tabs + 1, TabHistoryEntries = counts.TabHistoryEntries + historyLines };
                }
                else
                {
                    match = "(no tab collection found)";
                }

                break;

            case LegacyJsonFileKind.Drinks:
                if (LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(root, file.Kind, out var drinks, out match))
                {
                    recordCount = LegacyJsonPayloadExtractor.CountArrayElements(drinks);
                    counts = counts with { Drinks = counts.Drinks + recordCount };
                }
                else
                {
                    match = "(no drinks collection found)";
                }

                break;

            case LegacyJsonFileKind.Items:
                if (LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(root, file.Kind, out var items, out match))
                {
                    recordCount = LegacyJsonPayloadExtractor.CountArrayElements(items);
                    counts = counts with { Items = counts.Items + recordCount };
                }
                else
                {
                    match = "(no items collection found)";
                }

                break;

            case LegacyJsonFileKind.Categories:
                if (LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(root, file.Kind, out var cats, out match))
                {
                    recordCount = LegacyJsonPayloadExtractor.CountArrayElements(cats);
                    counts = counts with { Categories = counts.Categories + recordCount };
                }
                else
                {
                    match = "(no categories collection found)";
                }

                break;

            case LegacyJsonFileKind.Members:
                if (LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(root, file.Kind, out var members, out match))
                {
                    recordCount = LegacyJsonPayloadExtractor.CountArrayElements(members);
                    counts = counts with { Members = counts.Members + recordCount };
                }
                else
                {
                    match = "(no members collection found)";
                }

                break;

            case LegacyJsonFileKind.Bartenders:
                if (LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(root, file.Kind, out var bartenders, out match))
                {
                    recordCount = LegacyJsonPayloadExtractor.CountArrayElements(bartenders);
                    counts = counts with { Bartenders = counts.Bartenders + recordCount };
                }
                else
                {
                    match = "(no bartenders collection found)";
                }

                break;

            case LegacyJsonFileKind.PitstopSalesData:
                if (LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(root, file.Kind, out var pitstop, out match))
                {
                    recordCount = LegacyJsonPayloadExtractor.CountArrayElements(pitstop);
                    counts = counts with { PitstopSales = counts.PitstopSales + recordCount };
                }
                else
                {
                    match = "(no pitstop sales collection found)";
                }

                break;

            default:
                match = "(not mapped for preview)";
                break;
        }

        return new MigrationFilePreviewDiagnostic
        {
            FullPath = file.FullPath,
            RelativePath = file.RelativePath,
            FileKind = file.Kind,
            RootJsonType = rootType,
            MatchedCollectionPath = match,
            RecordCount = recordCount,
            ParseError = null,
            CountedAsUnreadableImportFailure = false,
            IsZeroCountTabularWarning = IsTabularMigrationKind(file.Kind) && recordCount == 0,
        };
    }

    public async Task<MigrationValidationResult> ValidateAsync(LegacyJsonDetectionResult detection, CancellationToken cancellationToken = default)
    {
        var issues = new List<MigrationValidationIssue>();

        if (!detection.HasAnyFiles)
        {
            issues.Add(new MigrationValidationIssue
            {
                Severity = MigrationValidationSeverity.Warning,
                Message = "No supported legacy JSON files were detected in the selected folder.",
                SourceFile = detection.RootFolder,
            });
        }

        if (string.IsNullOrWhiteSpace(detection.RootFolder) || !Directory.Exists(detection.RootFolder))
        {
            issues.Add(new MigrationValidationIssue
            {
                Severity = MigrationValidationSeverity.Error,
                Message = "The selected folder is missing or inaccessible.",
                SourceFile = detection.RootFolder,
            });
        }

        foreach (var file in detection.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(file.FullPath))
            {
                issues.Add(new MigrationValidationIssue
                {
                    Severity = MigrationValidationSeverity.Error,
                    Message = "Legacy file no longer exists on disk.",
                    SourceFile = file.FullPath,
                    FileKind = file.Kind,
                });
                continue;
            }

            var (doc, error) = await LegacyJsonDocumentReader.TryLoadDocumentAsync(file.FullPath, cancellationToken).ConfigureAwait(false);
            if (doc is null || error is not null)
            {
                var isConfigKind = file.Kind is LegacyJsonFileKind.SettingsOrConfig or LegacyJsonFileKind.SquareConfig;
                issues.Add(new MigrationValidationIssue
                {
                    Severity = isConfigKind ? MigrationValidationSeverity.Warning : MigrationValidationSeverity.Error,
                    Message = $"JSON could not be parsed: {error}",
                    SourceFile = file.FullPath,
                    FileKind = file.Kind,
                });
                continue;
            }

            doc.Dispose();

            issues.Add(new MigrationValidationIssue
            {
                Severity = MigrationValidationSeverity.Information,
                Message = "JSON parsed successfully.",
                SourceFile = file.FullPath,
                FileKind = file.Kind,
            });
        }

        return new MigrationValidationResult { Issues = issues };
    }

    public Task<string> CreateBackupAsync(LegacyJsonDetectionResult detection, Guid runId, CancellationToken cancellationToken = default)
        => _backup.CreateBackupSnapshotAsync(detection.RootFolder, detection, runId, cancellationToken);

    public async Task<MigrationImportResult> ImportAsync(
        LegacyJsonDetectionResult detection,
        MigrationRunContext context,
        string? backupFolder,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var run = new MigrationRunContext
        {
            RunId = context.RunId,
            SourceRootFolder = context.SourceRootFolder,
            StartedAtUtc = started,
            CompletedAtUtc = started,
            TreatWarningsAsErrors = context.TreatWarningsAsErrors,
            SkipIfAlreadyImported = context.SkipIfAlreadyImported,
        };

        _paths.EnsureDirectories();
        var logPath = Path.Combine(_paths.MigrationLogsFolder, $"{run.RunId:N}.log");

        using var logger = new FileMigrationLogger(logPath);
        logger.LogInformation($"[IMPORT] Migration run {run.RunId:N} starting for '{detection.RootFolder}'.");

        var segments = new List<MigrationSegmentResult>();
        var globalFailures = new List<MigrationImportFailure>();
        var runId = run.RunId;

        try
        {
            _migrationRunJournal.RecordRunStarted(runId, detection.RootFolder);
        }
        catch (Exception ex)
        {
            logger.LogError($"[FAILED] Could not write MigrationRuns start row: {ex.Message}");
        }

        try
        {
            var autoZip = await _dbBackup.CreateAutomaticBackupAsync("Before JSON migration", cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(autoZip))
            {
                logger.LogInformation($"[BACKUP] SQLite backup before import: {autoZip}");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[BACKUP] Could not create SQLite backup before import: {ex.Message}");
        }

        try
        {
            foreach (var file in detection.Files
                         .OrderBy(f => ImportKindOrdinal(f.Kind))
                         .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var hash = await LegacyJsonDocumentReader.ComputeSha256HexAsync(file.FullPath, cancellationToken).ConfigureAwait(false);

                    if (run.SkipIfAlreadyImported && await _fingerprints.WasSuccessfullyImportedAsync(file.Kind, file.FullPath, hash, cancellationToken).ConfigureAwait(false))
                    {
                        logger.LogInformation($"[SKIPPED_DUPLICATE] file='{file.RelativePath}' kind={file.Kind} fingerprint_sha256={hash} (unchanged file already imported successfully).");
                        segments.Add(new MigrationSegmentResult
                        {
                            Kind = file.Kind,
                            Attempted = 0,
                            Imported = 0,
                            SkippedDuplicate = 1,
                            Failures = [],
                        });
                        continue;
                    }

                    var segment = await ImportSingleFileAsync(file, logger, cancellationToken).ConfigureAwait(false);
                    segments.Add(segment);

                    if (segment.Failures.Count == 0 && segment.Imported > 0)
                    {
                        await _fingerprints.MarkSuccessfullyImportedAsync(file.Kind, file.FullPath, hash, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"[FAILED] Unhandled exception importing '{file.RelativePath}' ({file.Kind}): {ex}");
                    globalFailures.Add(new MigrationImportFailure
                    {
                        Message = ex.Message,
                        FileKind = file.Kind,
                        SourceFile = file.FullPath,
                    });
                }
            }
        }
        finally
        {
            try
            {
                var importedSum = segments.Sum(s => s.Imported);
                var skippedDup = segments.Sum(s => s.SkippedDuplicate);
                var failCount = segments.Sum(s => s.Failures.Count) + globalFailures.Count;
                var runNotes =
                    $"files={segments.Count}, imported_rows={importedSum}, skipped_duplicate_files={skippedDup}, failures={failCount}";
                _migrationRunJournal.RecordRunCompleted(runId, runNotes);
            }
            catch (Exception ex)
            {
                logger.LogError($"[FAILED] Could not write MigrationRuns completion row: {ex.Message}");
            }
        }

        var completed = DateTimeOffset.UtcNow;
        var finishedRun = run.WithCompletedUtc(completed);
        logger.LogInformation($"[IMPORTED] Migration run {finishedRun.RunId:N} completed.");
        _pinCache.Refresh(cancellationToken);
        CopySourceFilesToImportsArchive(detection, finishedRun.RunId, logger);

        return new MigrationImportResult
        {
            Run = finishedRun,
            Segments = segments,
            GlobalFailures = globalFailures,
            BackupFolder = backupFolder,
            LogFilePath = logPath,
        };
    }

    private void CopySourceFilesToImportsArchive(
        LegacyJsonDetectionResult detection,
        Guid runId,
        IMigrationLogger logger)
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var destRoot = Path.Combine(_paths.ImportsFolder, $"{stamp}_{runId:N}");
            Directory.CreateDirectory(destRoot);

            foreach (var file in detection.Files)
            {
                if (!File.Exists(file.FullPath))
                {
                    continue;
                }

                var rel = string.IsNullOrWhiteSpace(file.RelativePath)
                    ? Path.GetFileName(file.FullPath)
                    : file.RelativePath;
                var dest = Path.Combine(destRoot, rel);
                var destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(file.FullPath, dest, overwrite: true);
            }

            logger.LogInformation($"[IMPORT] Copied source JSON files to {destRoot}");
        }
        catch (Exception ex)
        {
            logger.LogWarning($"[IMPORT] Could not copy source files to Imports folder: {ex.Message}");
        }
    }

    private async Task<MigrationSegmentResult> ImportSingleFileAsync(
        LegacyDetectedFile file,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        var failures = new List<MigrationImportFailure>();

        var (doc, error) = await LegacyJsonDocumentReader.TryLoadDocumentAsync(file.FullPath, cancellationToken).ConfigureAwait(false);
        if (doc is null || error is not null)
        {
            failures.Add(new MigrationImportFailure
            {
                Message = error ?? "Unknown JSON parse failure.",
                FileKind = file.Kind,
                SourceFile = file.FullPath,
            });

            var parseFail = new MigrationSegmentResult
            {
                Kind = file.Kind,
                Attempted = 0,
                Imported = 0,
                SkippedDuplicate = 0,
                Failures = failures,
            };
            LogFailureRecords(file, parseFail.Failures, logger);
            return parseFail;
        }

        using (doc)
        {
            var segment = file.Kind switch
            {
                LegacyJsonFileKind.Tabs => await ImportTabsAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                LegacyJsonFileKind.Members => await ImportMembersAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                LegacyJsonFileKind.Categories => await ImportCategoriesAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                LegacyJsonFileKind.Items => await ImportItemsAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                LegacyJsonFileKind.Drinks => await ImportDrinksAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                LegacyJsonFileKind.Bartenders => await ImportBartendersAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                LegacyJsonFileKind.PitstopSalesData => await ImportPitstopAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                LegacyJsonFileKind.SquareConfig => await ImportSquareAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                LegacyJsonFileKind.SettingsOrConfig => await ImportSettingsAsync(file, doc, failures, logger, cancellationToken).ConfigureAwait(false),
                _ => UnknownKind(file, failures),
            };
            LogFailureRecords(file, segment.Failures, logger);
            return segment;
        }
    }

    private static void LogFailureRecords(LegacyDetectedFile file, IReadOnlyList<MigrationImportFailure> failures, IMigrationLogger logger)
    {
        foreach (var f in failures)
        {
            var ptr = string.IsNullOrEmpty(f.JsonPointer) ? string.Empty : $" json={f.JsonPointer}";
            logger.LogError($"[FAILED] file='{file.RelativePath}' kind={file.Kind}{ptr}: {f.Message}");
        }
    }

    private static void EnsureStableTabIds(IReadOnlyList<LegacyTabDto> items, IMigrationLogger logger)
    {
        foreach (var t in items)
        {
            if (!string.IsNullOrWhiteSpace(t.Id))
            {
                continue;
            }

            var synthetic = LegacyStableId.ForTab(t);
            t.Id = synthetic;
            logger.LogWarning($"[MISSING_FIELD] Tab record had no Id; assigned stable LegacyId '{synthetic}'. [RAW_JSON_FALLBACK] Row RawJson column stores full legacy payload.");
        }
    }

    private static void EnsureStableMemberIds(IReadOnlyList<LegacyMemberDto> items, IMigrationLogger logger)
    {
        foreach (var t in items)
        {
            if (!string.IsNullOrWhiteSpace(t.Id))
            {
                continue;
            }

            var synthetic = LegacyStableId.ForMember(t);
            t.Id = synthetic;
            logger.LogWarning($"[MISSING_FIELD] Member record had no Id; assigned stable LegacyId '{synthetic}'. [RAW_JSON_FALLBACK] Row RawJson stores payload.");
        }
    }

    private static void EnsureStableCategoryIds(IReadOnlyList<LegacyCategoryDto> items, IMigrationLogger logger)
    {
        foreach (var t in items)
        {
            if (!string.IsNullOrWhiteSpace(t.Id))
            {
                continue;
            }

            var synthetic = LegacyStableId.ForCategory(t);
            t.Id = synthetic;
            logger.LogWarning($"[MISSING_FIELD] Category record had no Id; assigned stable LegacyId '{synthetic}'. [RAW_JSON_FALLBACK] Row RawJson stores payload.");
        }
    }

    private static void EnsureStableItemIds(IReadOnlyList<LegacyItemDto> items, IMigrationLogger logger)
    {
        foreach (var t in items)
        {
            if (!string.IsNullOrWhiteSpace(t.Id))
            {
                continue;
            }

            var synthetic = LegacyStableId.ForItem(t);
            t.Id = synthetic;
            logger.LogWarning($"[MISSING_FIELD] Item record had no Id; assigned stable LegacyId '{synthetic}'. [RAW_JSON_FALLBACK] Row RawJson stores payload.");
        }
    }

    private static void EnsureStableDrinkIds(IReadOnlyList<LegacyDrinkDto> items, IMigrationLogger logger)
    {
        foreach (var t in items)
        {
            if (!string.IsNullOrWhiteSpace(t.Id))
            {
                continue;
            }

            var synthetic = LegacyStableId.ForDrink(t);
            t.Id = synthetic;
            logger.LogWarning($"[MISSING_FIELD] Drink record had no Id; assigned stable LegacyId '{synthetic}'. [RAW_JSON_FALLBACK] Row RawJson stores payload.");
        }
    }

    private static void EnsureStableBartenderIds(IReadOnlyList<LegacyBartenderDto> items, IMigrationLogger logger)
    {
        foreach (var t in items)
        {
            if (!string.IsNullOrWhiteSpace(t.Id))
            {
                continue;
            }

            var synthetic = LegacyStableId.ForBartender(t);
            t.Id = synthetic;
            logger.LogWarning($"[MISSING_FIELD] Bartender record had no Id; assigned stable LegacyId '{synthetic}'. [RAW_JSON_FALLBACK] Row RawJson stores payload.");
        }
    }

    private static void EnsureStablePitstopIds(IReadOnlyList<LegacyPitstopSaleDto> items, IMigrationLogger logger)
    {
        foreach (var t in items)
        {
            if (!string.IsNullOrWhiteSpace(t.Id))
            {
                continue;
            }

            var synthetic = LegacyStableId.ForPitstopSale(t);
            t.Id = synthetic;
            logger.LogWarning($"[MISSING_FIELD] Pitstop sale had no Id; assigned stable LegacyId '{synthetic}'. [RAW_JSON_FALLBACK] Row RawJson stores payload.");
        }
    }

    private static MigrationSegmentResult UnknownKind(LegacyDetectedFile file, List<MigrationImportFailure> failures)
    {
        failures.Add(new MigrationImportFailure
        {
            Message = "Unsupported legacy file kind.",
            FileKind = file.Kind,
            SourceFile = file.FullPath,
        });

        return new MigrationSegmentResult
        {
            Kind = file.Kind,
            Attempted = 0,
            Imported = 0,
            SkippedDuplicate = 0,
            Failures = failures,
        };
    }

    private async Task<MigrationSegmentResult> ImportTabsAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        var root = doc.RootElement;

        if (LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(root, LegacyJsonFileKind.Tabs, out var array, out _))
        {
            var (items, attempted, imported, parseFailures) = DeserializeElements<LegacyTabDto>(array);
            failures.AddRange(parseFailures);

            if (items.Count > 0)
            {
                EnsureStableTabIds(items, logger);
                await _tabs.ImportTabsAsync(items, cancellationToken).ConfigureAwait(false);
                logger.LogInformation($"[IMPORTED] {items.Count} tab row(s) upserted from '{file.RelativePath}'.");
            }

            return Segment(LegacyJsonFileKind.Tabs, attempted, imported, failures);
        }

        if (LegacyJsonPayloadExtractor.TryRecognizeSingleTabObject(root, out _))
        {
            try
            {
                var single = JsonSerializer.Deserialize<LegacyTabDto>(root.GetRawText(), MigrationJsonDefaults.SerializerOptions);
                if (single is null)
                {
                    failures.Add(new MigrationImportFailure { Message = "Single tab object deserialized to null.", SourceFile = file.FullPath, FileKind = file.Kind });
                    return Segment(LegacyJsonFileKind.Tabs, 1, 0, failures);
                }

                EnsureStableTabIds(new[] { single }, logger);
                await _tabs.ImportTabsAsync(new List<LegacyTabDto> { single }, cancellationToken).ConfigureAwait(false);
                logger.LogInformation($"[IMPORTED] 1 tab (single object) upserted from '{file.RelativePath}'.");
                return Segment(LegacyJsonFileKind.Tabs, 1, 1, failures);
            }
            catch (Exception ex)
            {
                failures.Add(new MigrationImportFailure { Message = ex.Message, SourceFile = file.FullPath, FileKind = file.Kind });
                return Segment(LegacyJsonFileKind.Tabs, 1, 0, failures);
            }
        }

        failures.Add(new MigrationImportFailure { Message = "Could not locate tabs collection in JSON.", SourceFile = file.FullPath, FileKind = file.Kind });
        return Segment(LegacyJsonFileKind.Tabs, 0, 0, failures);
    }

    private async Task<MigrationSegmentResult> ImportMembersAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        if (!LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(doc.RootElement, file.Kind, out var array, out _))
        {
            failures.Add(new MigrationImportFailure { Message = "Could not locate members collection in JSON.", SourceFile = file.FullPath, FileKind = file.Kind });
            return Segment(LegacyJsonFileKind.Members, 0, 0, failures);
        }

        var (items, attempted, imported, parseFailures) = DeserializeElements<LegacyMemberDto>(array);
        failures.AddRange(parseFailures);

        if (items.Count > 0)
        {
            EnsureStableMemberIds(items, logger);
            await _members.ImportMembersAsync(items, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"[IMPORTED] {items.Count} member row(s) upserted from '{file.RelativePath}'.");
        }

        return Segment(LegacyJsonFileKind.Members, attempted, imported, failures);
    }

    private async Task<MigrationSegmentResult> ImportCategoriesAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        if (!LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(doc.RootElement, LegacyJsonFileKind.Categories, out var array, out _))
        {
            failures.Add(new MigrationImportFailure { Message = "Could not locate categories collection in JSON.", SourceFile = file.FullPath, FileKind = file.Kind });
            return Segment(LegacyJsonFileKind.Categories, 0, 0, failures);
        }

        var (items, attempted, imported, parseFailures) = DeserializeCategoryRecords(array);
        failures.AddRange(parseFailures);

        if (items.Count > 0)
        {
            EnsureStableCategoryIds(items, logger);
            await _categories.ImportCategoriesAsync(items, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"[IMPORTED] {items.Count} categor(y/ies) upserted from '{file.RelativePath}'.");
        }

        return Segment(LegacyJsonFileKind.Categories, attempted, imported, failures);
    }

    private async Task<MigrationSegmentResult> ImportItemsAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        if (!LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(doc.RootElement, file.Kind, out var array, out _))
        {
            failures.Add(new MigrationImportFailure { Message = "Could not locate items collection in JSON.", SourceFile = file.FullPath, FileKind = file.Kind });
            return Segment(LegacyJsonFileKind.Items, 0, 0, failures);
        }

        var (items, attempted, imported, parseFailures) = DeserializeItemRecords(array);
        failures.AddRange(parseFailures);

        if (items.Count > 0)
        {
            EnsureStableItemIds(items, logger);
            await _items.ImportItemsAsync(items, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"[IMPORTED] {items.Count} stock/item row(s) upserted from '{file.RelativePath}'.");
        }

        return Segment(LegacyJsonFileKind.Items, attempted, imported, failures);
    }

    private async Task<MigrationSegmentResult> ImportDrinksAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        if (!LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(doc.RootElement, file.Kind, out var array, out _))
        {
            failures.Add(new MigrationImportFailure { Message = "Expected a JSON array (or object containing an array) for drinks.", SourceFile = file.FullPath, FileKind = file.Kind });
            return Segment(LegacyJsonFileKind.Drinks, 0, 0, failures);
        }

        var (items, attempted, imported, parseFailures) = DeserializeDrinkRecords(array);
        failures.AddRange(parseFailures);

        if (items.Count > 0)
        {
            EnsureStableDrinkIds(items, logger);
            await _drinks.ImportDrinksAsync(items, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"[IMPORTED] {items.Count} drink row(s) upserted as Items from '{file.RelativePath}'.");
        }

        return Segment(LegacyJsonFileKind.Drinks, attempted, imported, failures);
    }

    private async Task<MigrationSegmentResult> ImportBartendersAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        if (!LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(doc.RootElement, file.Kind, out var array, out _))
        {
            failures.Add(new MigrationImportFailure { Message = "Could not locate bartenders collection in JSON.", SourceFile = file.FullPath, FileKind = file.Kind });
            return Segment(LegacyJsonFileKind.Bartenders, 0, 0, failures);
        }

        var (items, attempted, imported, parseFailures) = DeserializeElements<LegacyBartenderDto>(array);
        failures.AddRange(parseFailures);

        if (items.Count > 0)
        {
            EnsureStableBartenderIds(items, logger);
            await _bartenders.ImportBartendersAsync(items, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"[IMPORTED] {items.Count} bartender row(s) upserted from '{file.RelativePath}'.");
        }

        return Segment(LegacyJsonFileKind.Bartenders, attempted, imported, failures);
    }

    private async Task<MigrationSegmentResult> ImportPitstopAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        if (!LegacyJsonPayloadExtractor.TryGetPrimaryDataArray(doc.RootElement, file.Kind, out var array, out _))
        {
            failures.Add(new MigrationImportFailure { Message = "Could not locate pitstop sales collection in JSON.", SourceFile = file.FullPath, FileKind = file.Kind });
            return Segment(LegacyJsonFileKind.PitstopSalesData, 0, 0, failures);
        }

        var (items, attempted, imported, parseFailures) = DeserializeElements<LegacyPitstopSaleDto>(array);
        failures.AddRange(parseFailures);

        if (items.Count > 0)
        {
            EnsureStablePitstopIds(items, logger);
            await _pitstopSales.ImportSalesAsync(items, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"[IMPORTED] {items.Count} pitstop sale row(s) upserted from '{file.RelativePath}'.");
        }

        return Segment(LegacyJsonFileKind.PitstopSalesData, attempted, imported, failures);
    }

    private async Task<MigrationSegmentResult> ImportSquareAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = JsonSerializer.Deserialize<LegacySquareConfigDto>(doc.RootElement.GetRawText(), MigrationJsonDefaults.SerializerOptions);
            if (model is null)
            {
                failures.Add(new MigrationImportFailure { Message = "Square config deserialized to null.", SourceFile = file.FullPath, FileKind = file.Kind });
                return Segment(LegacyJsonFileKind.SquareConfig, 1, 0, failures);
            }

            await _square.ImportSquareConfigAsync(model, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"[IMPORTED] Square config upserted from '{file.RelativePath}'.");
            return Segment(LegacyJsonFileKind.SquareConfig, 1, 1, failures);
        }
        catch (Exception ex)
        {
            failures.Add(new MigrationImportFailure { Message = ex.Message, SourceFile = file.FullPath, FileKind = file.Kind });
            return Segment(LegacyJsonFileKind.SquareConfig, 1, 0, failures);
        }
    }

    private async Task<MigrationSegmentResult> ImportSettingsAsync(
        LegacyDetectedFile file,
        JsonDocument doc,
        List<MigrationImportFailure> failures,
        IMigrationLogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await _settings.ImportSettingsDocumentAsync(file.RelativePath, doc, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"[IMPORTED] Settings document upserted '{file.RelativePath}'.");
            return Segment(LegacyJsonFileKind.SettingsOrConfig, 1, 1, failures);
        }
        catch (Exception ex)
        {
            failures.Add(new MigrationImportFailure { Message = ex.Message, SourceFile = file.FullPath, FileKind = file.Kind });
            return Segment(LegacyJsonFileKind.SettingsOrConfig, 1, 0, failures);
        }
    }

    private static MigrationSegmentResult Segment(LegacyJsonFileKind kind, int attempted, int imported, List<MigrationImportFailure> failures)
    {
        return new MigrationSegmentResult
        {
            Kind = kind,
            Attempted = attempted,
            Imported = imported,
            SkippedDuplicate = 0,
            Failures = failures,
        };
    }

    private static (List<T> Items, int Attempted, int Imported, List<MigrationImportFailure> Failures) DeserializeElements<T>(JsonElement array)
        where T : class
    {
        var items = new List<T>();
        var failures = new List<MigrationImportFailure>();
        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            try
            {
                var model = JsonSerializer.Deserialize<T>(element.GetRawText(), MigrationJsonDefaults.SerializerOptions);
                if (model is null)
                {
                    failures.Add(new MigrationImportFailure { Message = "Element deserialized to null.", JsonPointer = $"/{index}" });
                }
                else
                {
                    items.Add(model);
                }
            }
            catch (Exception ex)
            {
                failures.Add(new MigrationImportFailure { Message = ex.Message, JsonPointer = $"/{index}" });
            }

            index++;
        }

        var attempted = index;
        var imported = items.Count;
        return (items, attempted, imported, failures);
    }

    private static (List<LegacyItemDto> Items, int Attempted, int Imported, List<MigrationImportFailure> Failures) DeserializeItemRecords(JsonElement array)
    {
        var items = new List<LegacyItemDto>();
        var failures = new List<MigrationImportFailure>();
        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            try
            {
                var model = JsonSerializer.Deserialize<LegacyItemDto>(element.GetRawText(), MigrationJsonDefaults.SerializerOptions);
                if (model is null)
                {
                    failures.Add(new MigrationImportFailure { Message = "Element deserialized to null.", JsonPointer = $"/{index}" });
                }
                else
                {
                    LegacyDtoCoalescing.ApplyLooseItemFields(model, element);
                    LegacyDtoCoalescing.FinalizePosBarStockItem(model);
                    items.Add(model);
                }
            }
            catch (Exception ex)
            {
                failures.Add(new MigrationImportFailure { Message = ex.Message, JsonPointer = $"/{index}" });
            }

            index++;
        }

        return (items, index, items.Count, failures);
    }

    private static (List<LegacyDrinkDto> Items, int Attempted, int Imported, List<MigrationImportFailure> Failures) DeserializeDrinkRecords(JsonElement array)
    {
        var items = new List<LegacyDrinkDto>();
        var failures = new List<MigrationImportFailure>();
        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            try
            {
                var model = JsonSerializer.Deserialize<LegacyDrinkDto>(element.GetRawText(), MigrationJsonDefaults.SerializerOptions);
                if (model is null)
                {
                    failures.Add(new MigrationImportFailure { Message = "Element deserialized to null.", JsonPointer = $"/{index}" });
                }
                else
                {
                    LegacyDtoCoalescing.ApplyLooseDrinkFields(model, element);
                    items.Add(model);
                }
            }
            catch (Exception ex)
            {
                failures.Add(new MigrationImportFailure { Message = ex.Message, JsonPointer = $"/{index}" });
            }

            index++;
        }

        return (items, index, items.Count, failures);
    }

    private static (List<LegacyCategoryDto> Items, int Attempted, int Imported, List<MigrationImportFailure> Failures) DeserializeCategoryRecords(JsonElement array)
    {
        var items = new List<LegacyCategoryDto>();
        var failures = new List<MigrationImportFailure>();
        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            try
            {
                var model = JsonSerializer.Deserialize<LegacyCategoryDto>(element.GetRawText(), MigrationJsonDefaults.SerializerOptions);
                if (model is null)
                {
                    failures.Add(new MigrationImportFailure { Message = "Element deserialized to null.", JsonPointer = $"/{index}" });
                }
                else
                {
                    LegacyDtoCoalescing.ApplyLooseCategoryFields(model, element);
                    items.Add(model);
                }
            }
            catch (Exception ex)
            {
                failures.Add(new MigrationImportFailure { Message = ex.Message, JsonPointer = $"/{index}" });
            }

            index++;
        }

        return (items, index, items.Count, failures);
    }
}
