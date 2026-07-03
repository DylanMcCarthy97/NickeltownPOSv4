using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class BackupViewModel : SettingsSubViewModelBase
{
    private readonly IBackupService _backup;
    private readonly IWindowHandleProvider _window;
    private readonly AppDatabase _database;

    private string _lastBackupPath = string.Empty;

    public BackupViewModel(
        INavigationService navigation,
        IBackupService backup,
        IWindowHandleProvider window,
        AppDatabase database)
        : base(navigation)
    {
        _backup = backup;
        _window = window;
        _database = database;

        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => !IsBusy);
    }

    public string DatabaseFilePath => _database.DatabaseFilePath;

    public string LastBackupPath
    {
        get => _lastBackupPath;
        private set => SetProperty(ref _lastBackupPath, value);
    }

    public IAsyncRelayCommand CreateBackupCommand { get; }

    private async Task CreateBackupAsync()
    {
        var folder = await PickFolderAsync().ConfigureAwait(true);
        if (string.IsNullOrEmpty(folder))
        {
            SetStatus("Backup canceled (no folder selected).");
            return;
        }

        try
        {
            IsBusy = true;
            CreateBackupCommand.NotifyCanExecuteChanged();
            SetStatus($"Creating backup in {folder}...");
            var zipPath = await _backup.CreateBackupAsync(folder).ConfigureAwait(true);
            LastBackupPath = zipPath;
            SetStatus($"Backup created: {zipPath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Backup failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            CreateBackupCommand.NotifyCanExecuteChanged();
        }
    }

    private Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add("*");

        var hwnd = _window.WindowHandle;
        if (hwnd != 0)
        {
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        var tcs = new TaskCompletionSource<string?>();
        var op = picker.PickSingleFolderAsync();
        op.Completed = (operation, status) =>
        {
            try
            {
                if (status == Windows.Foundation.AsyncStatus.Completed)
                {
                    var folder = operation.GetResults();
                    tcs.TrySetResult(folder?.Path);
                }
                else
                {
                    tcs.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        return tcs.Task;
    }
}
