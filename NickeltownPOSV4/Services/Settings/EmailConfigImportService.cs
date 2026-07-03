using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace NickeltownPOSV4.Services.Settings;

public sealed class EmailConfigImportService : IEmailConfigImportService
{
    private readonly IEmailConfigService _target;

    public EmailConfigImportService(IEmailConfigService target) => _target = target;

    public string? TryFindLegacyConfigPath()
    {
        foreach (var p in BuildCandidatePaths())
        {
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
            {
                return p;
            }
        }

        return null;
    }

    public Task<EmailImportResult> ImportAsync(bool overwriteExisting, CancellationToken cancellationToken = default)
    {
        var path = TryFindLegacyConfigPath();
        if (string.IsNullOrEmpty(path))
        {
            return Task.FromResult(new EmailImportResult(false, null, "No POSBarV2 email_config.json found in any standard location."));
        }

        return ImportFromPathAsync(path, overwriteExisting, cancellationToken);
    }

    public async Task<EmailImportResult> ImportFromPathAsync(
        string filePath,
        bool overwriteExisting,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new EmailImportResult(false, filePath, "Email config file was not found.");
            }

            if (!overwriteExisting)
            {
                var existing = await _target.LoadAsync(cancellationToken).ConfigureAwait(false);
                if (existing is not null
                    && (!string.IsNullOrWhiteSpace(existing.SenderEmail)
                        || !string.IsNullOrWhiteSpace(existing.SenderPassword)
                        || existing.RecipientEmails.Any(r => !string.IsNullOrWhiteSpace(r))))
                {
                    return new EmailImportResult(false, filePath, "Existing email configuration found. Skipped to avoid overwriting.");
                }
            }

            string json;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream))
            {
                json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }

            var legacy = JsonSerializer.Deserialize<LegacyEmailConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (legacy is null)
            {
                return new EmailImportResult(false, filePath, "Email config file was empty or unreadable.");
            }

            var config = MapLegacy(legacy);

            await _target.SaveAsync(config, cancellationToken).ConfigureAwait(false);
            return new EmailImportResult(true, filePath, $"Imported from {filePath}.");
        }
        catch (Exception ex)
        {
            return new EmailImportResult(false, filePath, $"Import failed: {ex.Message}");
        }
    }

    internal static AppEmailConfig MapLegacy(LegacyEmailConfig legacy)
    {
        var recipients = new List<string>();

        if (legacy.RecipientEmails is not null)
        {
            recipients.AddRange(legacy.RecipientEmails
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(legacy.RecipientEmail)
            && !recipients.Contains(legacy.RecipientEmail.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            recipients.Add(legacy.RecipientEmail.Trim());
        }

        return new AppEmailConfig
        {
            SmtpServer = legacy.SmtpServer?.Trim() ?? "smtp.gmail.com",
            SmtpPort = legacy.SmtpPort > 0 ? legacy.SmtpPort : 587,
            EnableSsl = legacy.EnableSsl,
            SenderEmail = legacy.SenderEmail?.Trim() ?? string.Empty,
            SenderPassword = legacy.SenderPassword ?? string.Empty,
            SenderName = legacy.SenderName?.Trim() ?? "Nickeltown POS",
            RecipientEmails = recipients
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    private static IEnumerable<string> BuildCandidatePaths()
    {
        // Working directory + base directory (most common for portable installs)
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "email_config.json");
        yield return Path.Combine(Environment.CurrentDirectory, "email_config.json");

        // Original v2 build output directories (in case running side-by-side during migration)
        yield return @"D:\POSBAR CLEAN\POSBarV2\email_config.json";
        yield return @"D:\POSBAR CLEAN\POSBarV2\bin\Debug\net8.0-windows\email_config.json";
        yield return @"D:\POSBAR CLEAN\POSBarV2\bin\Release\net8.0-windows\win-x64\email_config.json";
        yield return @"D:\POSBAR CLEAN\POSBarV2\bin\Release\net8.0-windows\win-x64\publish\email_config.json";
        yield return @"D:\POSBAR CLEAN\POSBarV2\finished\email_config.json";

        // %AppData% / %ProgramData% / %LocalAppData% style locations that the v2 DataPathManager often used
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "POSBar", "email_config.json");
            yield return Path.Combine(appData, "NickeltownPOS", "email_config.json");
            yield return Path.Combine(appData, "Nickeltown Flounderers", "email_config.json");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "POSBar", "email_config.json");
            yield return Path.Combine(localAppData, "NickeltownPOS", "email_config.json");
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
        {
            yield return Path.Combine(programData, "POSBar", "email_config.json");
            yield return Path.Combine(programData, "NickeltownPOS", "email_config.json");
        }

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(docs))
        {
            yield return Path.Combine(docs, "POSBar", "email_config.json");
            yield return Path.Combine(docs, "email_config.json");
        }
    }

    internal sealed class LegacyEmailConfig
    {
        public string? SmtpServer { get; set; }

        public int SmtpPort { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        public string? SenderEmail { get; set; }

        public string? SenderPassword { get; set; }

        public string? RecipientEmail { get; set; }

        public List<string>? RecipientEmails { get; set; }

        public string? SenderName { get; set; }
    }
}

public sealed class WinUIEmailConfigFilePicker : IEmailConfigFilePicker
{
    private readonly IWindowHandleProvider _window;

    public WinUIEmailConfigFilePicker(IWindowHandleProvider window) => _window = window;

    public Task<string?> PickEmailConfigFileAsync(string? suggestedFilePath = null)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };

        picker.FileTypeFilter.Add(".json");

        var hwnd = _window.WindowHandle;
        if (hwnd != 0)
        {
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return WaitForStorageFileAsync(picker.PickSingleFileAsync());
    }

    private static Task<string?> WaitForStorageFileAsync(IAsyncOperation<StorageFile> operation)
    {
        var tcs = new TaskCompletionSource<string?>();

        operation.Completed = (op, status) =>
        {
            switch (status)
            {
                case AsyncStatus.Completed:
                    tcs.TrySetResult(op.GetResults()?.Path);
                    break;
                case AsyncStatus.Canceled:
                    tcs.TrySetResult(null);
                    break;
                case AsyncStatus.Error:
                    tcs.TrySetException(op.ErrorCode as Exception ?? new InvalidOperationException("File picker failed."));
                    break;
                default:
                    tcs.TrySetResult(null);
                    break;
            }
        };

        return tcs.Task;
    }
}
