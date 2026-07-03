using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Migration;
using NickeltownPOSV4.Services.Migration;

namespace NickeltownPOSV4.ViewModels;

/// <summary>
/// Orchestrates the reusable V2→V4 migration wizard: folder selection, detection, preview, validation, backup, import, and summary.
/// </summary>
public sealed class MigrationWizardViewModel : ObservableViewModel
{
    private readonly IJsonImportService _jsonImport;
    private readonly IMigrationFolderPicker _folderPicker;
    private readonly IImportDatabaseStatistics _importStatistics;

    private string? _selectedFolder;
    private LegacyJsonDetectionResult? _detection;
    private MigrationPreviewCounts? _preview;

    private IReadOnlyList<MigrationFilePreviewDiagnostic> _previewDiagnostics = Array.Empty<MigrationFilePreviewDiagnostic>();
    private MigrationValidationResult? _validation;
    private string? _backupFolder;
    private Guid? _activeRunId;
    private MigrationImportResult? _lastImport;
    private MigrationSummary? _summary;
    private ImportVerificationSnapshot? _importVerification;
    private MigrationWizardStep _step = MigrationWizardStep.SelectV2Folder;
    private bool _isBusy;
    private string _statusMessage = "Choose the folder that contains exported Nickeltown JSON to begin.";

    public MigrationWizardViewModel(
        IJsonImportService jsonImport,
        IMigrationFolderPicker folderPicker,
        IImportDatabaseStatistics importStatistics)
    {
        _jsonImport = jsonImport;
        _folderPicker = folderPicker;
        _importStatistics = importStatistics;

        PickFolderCommand = new AsyncRelayCommand(PickFolderAsync);
        DetectCommand = new AsyncRelayCommand(DetectAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(SelectedFolder));
        BuildPreviewCommand = new AsyncRelayCommand(BuildPreviewAsync, () => !IsBusy && _detection?.HasAnyFiles == true);
        ValidateCommand = new AsyncRelayCommand(ValidateAsync, () => !IsBusy && _detection?.HasAnyFiles == true);
        BackupCommand = new AsyncRelayCommand(BackupAsync, () => !IsBusy && _detection?.HasAnyFiles == true);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => !IsBusy && _detection?.HasAnyFiles == true && !string.IsNullOrWhiteSpace(_backupFolder));
        BuildSummaryCommand = new AsyncRelayCommand(BuildSummaryAsync, () => _lastImport is not null);
    }

    public IAsyncRelayCommand PickFolderCommand { get; }

    public IAsyncRelayCommand DetectCommand { get; }

    public IAsyncRelayCommand BuildPreviewCommand { get; }

    public IAsyncRelayCommand ValidateCommand { get; }

    public IAsyncRelayCommand BackupCommand { get; }

    public IAsyncRelayCommand ImportCommand { get; }

    public IAsyncRelayCommand BuildSummaryCommand { get; }

    public string? SelectedFolder
    {
        get => _selectedFolder;
        private set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public LegacyJsonDetectionResult? Detection => _detection;

    public IReadOnlyList<LegacyDetectedFile> DetectedFiles =>
        Detection?.Files ?? Array.Empty<LegacyDetectedFile>();

    public MigrationPreviewCounts? Preview => _preview;

    public IReadOnlyList<MigrationFilePreviewDiagnostic> PreviewDiagnostics => _previewDiagnostics;

    public MigrationValidationResult? Validation => _validation;

    public IReadOnlyList<MigrationValidationIssue> ValidationIssues =>
        Validation?.Issues ?? Array.Empty<MigrationValidationIssue>();

    public string? BackupFolder
    {
        get => _backupFolder;
        private set
        {
            if (SetProperty(ref _backupFolder, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public MigrationImportResult? LastImport => _lastImport;

    public MigrationSummary? Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public ImportVerificationSnapshot? ImportVerification
    {
        get => _importVerification;
        private set => SetProperty(ref _importVerification, value);
    }

    public MigrationWizardStep Step
    {
        get => _step;
        private set => SetProperty(ref _step, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string DetectionSummaryText
    {
        get
        {
            if (_detection is null || !_detection.HasAnyFiles)
            {
                return "No legacy files detected yet.";
            }

            var sb = new StringBuilder();
            foreach (var group in _detection.Files.GroupBy(f => f.Kind).OrderBy(g => g.Key))
            {
                sb.AppendLine($"{group.Key}: {group.Count()} file(s)");
            }

            return sb.ToString().TrimEnd();
        }
    }

    private async Task PickFolderAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Waiting for folder selection…";
            var path = await _folderPicker.PickV2DataFolderAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusMessage = "Folder selection canceled.";
                return;
            }

            SelectedFolder = path;
            Step = MigrationWizardStep.DetectJsonFiles;
            StatusMessage = $"Selected folder: {path}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DetectAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedFolder))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Detecting legacy JSON files…";
            _detection = await _jsonImport.DetectAsync(SelectedFolder).ConfigureAwait(true);
            _previewDiagnostics = Array.Empty<MigrationFilePreviewDiagnostic>();
            OnPropertyChanged(nameof(Detection));
            OnPropertyChanged(nameof(DetectedFiles));
            OnPropertyChanged(nameof(PreviewDiagnostics));
            OnPropertyChanged(nameof(DetectionSummaryText));
            Step = MigrationWizardStep.PreviewCounts;
            StatusMessage = _detection.HasAnyFiles
                ? $"Detected {_detection.Files.Count} legacy JSON file(s)."
                : "No supported legacy JSON files were found.";
            RefreshCommandStates();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BuildPreviewAsync()
    {
        if (_detection is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Building preview counts…";
            var previewBuild = await _jsonImport.BuildPreviewAsync(_detection).ConfigureAwait(true);
            _preview = previewBuild.Counts;
            _previewDiagnostics = previewBuild.Diagnostics;
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(PreviewDiagnostics));
            Step = MigrationWizardStep.ValidateData;
            StatusMessage = "Preview counts updated.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ValidateAsync()
    {
        if (_detection is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Validating JSON…";
            _validation = await _jsonImport.ValidateAsync(_detection).ConfigureAwait(true);
            OnPropertyChanged(nameof(Validation));
            OnPropertyChanged(nameof(ValidationIssues));

            if (_validation.HasErrors)
            {
                StatusMessage = "Validation completed with errors.";
                Step = MigrationWizardStep.ValidateData;
            }
            else
            {
                StatusMessage = _validation.HasWarnings ? "Validation completed with warnings." : "Validation completed successfully.";
                Step = MigrationWizardStep.BackupOriginals;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BackupAsync()
    {
        if (_detection is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Creating backup snapshot (original files are never modified)…";
            _activeRunId = Guid.NewGuid();
            BackupFolder = await _jsonImport.CreateBackupAsync(_detection, _activeRunId.Value).ConfigureAwait(true);
            StatusMessage = $"Backup created at: {BackupFolder}";
            Step = MigrationWizardStep.ImportToSqlite;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ImportAsync()
    {
        if (_detection is null || string.IsNullOrWhiteSpace(BackupFolder))
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Importing legacy data into SQLite…";

            var run = new MigrationRunContext
            {
                RunId = _activeRunId ?? Guid.NewGuid(),
                SourceRootFolder = _detection.RootFolder,
                SkipIfAlreadyImported = true,
            };

            _lastImport = await _jsonImport.ImportAsync(_detection, run, BackupFolder).ConfigureAwait(true);
            OnPropertyChanged(nameof(LastImport));

            Summary = MigrationSummary.FromImport(_lastImport);
            ImportVerification = await _importStatistics.BuildVerificationAsync(_lastImport).ConfigureAwait(true);

            BuildSummaryCommand.NotifyCanExecuteChanged();

            Step = MigrationWizardStep.Summary;
            StatusMessage = "Import finished. Review SQLite verification, summary, and migration log path.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BuildSummaryAsync()
    {
        if (_lastImport is null)
        {
            return;
        }

        Summary = MigrationSummary.FromImport(_lastImport);
        ImportVerification = await _importStatistics.BuildVerificationAsync(_lastImport).ConfigureAwait(true);
    }

    private void RefreshCommandStates()
    {
        DetectCommand.NotifyCanExecuteChanged();
        BuildPreviewCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged();
        BackupCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
    }
}
