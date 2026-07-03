using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Settings;

public sealed class GoogleDriveBackupUploader
{
    private const string ApplicationName = "Nickeltown POS v4 Backup";
    private const string BackupFolderName = "POSBar Backups";
    private static readonly string[] Scopes = [DriveService.Scope.DriveFile];

    private DriveService? _driveService;

    public string CredentialsPath { get; set; } = Path.Combine(
        AppStoragePaths.GetDocumentsFolder(),
        AppStoragePaths.RootFolderName,
        "Config",
        "credentials.json");

    public string TokenStorePath { get; set; } = Path.Combine(
        AppStoragePaths.GetDocumentsFolder(),
        AppStoragePaths.RootFolderName,
        "Config",
        "google_token");

    public bool IsConfigured => File.Exists(CredentialsPath);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(CredentialsPath))
        {
            throw new FileNotFoundException(
                "Google Drive credentials not found. Place credentials.json in the application folder.",
                CredentialsPath);
        }

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromFile(CredentialsPath).Secrets,
            Scopes,
            "user",
            cancellationToken,
            new FileDataStore(TokenStorePath, true)).ConfigureAwait(false);

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
        });
    }

    public async Task<string> UploadBackupFileAsync(string zipPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("Backup zip not found.", zipPath);
        }

        if (_driveService is null)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        var folderId = await GetOrCreateBackupFolderAsync(cancellationToken).ConfigureAwait(false);
        var fileName = Path.GetFileName(zipPath);
        await using var stream = File.OpenRead(zipPath);
        var request = _driveService!.Files.Create(
            new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = [folderId],
            },
            stream,
            "application/zip");
        request.Fields = "id, name";
        var progress = await request.UploadAsync(cancellationToken).ConfigureAwait(false);
        if (progress.Status != UploadStatus.Completed)
        {
            throw new InvalidOperationException(progress.Exception?.Message ?? "Google Drive upload failed.");
        }

        return request.ResponseBody?.Id ?? string.Empty;
    }

    private async Task<string> GetOrCreateBackupFolderAsync(CancellationToken cancellationToken)
    {
        var list = _driveService!.Files.List();
        list.Q = $"mimeType='application/vnd.google-apps.folder' and name='{BackupFolderName}' and trashed=false";
        list.Fields = "files(id, name)";
        var result = await list.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        var existing = result.Files?.FirstOrDefault();
        if (existing is not null)
        {
            return existing.Id;
        }

        var folder = new Google.Apis.Drive.v3.Data.File
        {
            Name = BackupFolderName,
            MimeType = "application/vnd.google-apps.folder",
        };
        var created = await _driveService.Files.Create(folder).ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return created.Id;
    }
}