using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class EmailConfigViewModel : SettingsSubViewModelBase
{
    private readonly IEmailConfigService _config;
    private readonly IEmailSender _sender;
    private readonly IInputOverlayService _inputOverlay;
    private readonly IEmailConfigImportService _import;
    private readonly IEmailConfigFilePicker _filePicker;

    private string _smtpServer = "smtp.gmail.com";
    private int _smtpPort = 587;
    private bool _enableSsl = true;
    private string _senderEmail = string.Empty;
    private string _senderPassword = string.Empty;
    private string _senderName = "Nickeltown POS";
    private string _recipientList = string.Empty;

    public EmailConfigViewModel(
        INavigationService navigation,
        IEmailConfigService config,
        IEmailSender sender,
        IInputOverlayService inputOverlay,
        IEmailConfigImportService import,
        IEmailConfigFilePicker filePicker)
        : base(navigation)
    {
        _config = config;
        _sender = sender;
        _inputOverlay = inputOverlay;
        _import = import;
        _filePicker = filePicker;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        SendTestCommand = new AsyncRelayCommand(SendTestAsync, () => !IsBusy);
        ImportFromV2Command = new AsyncRelayCommand(ImportFromV2Async, () => !IsBusy);

        EditSmtpServerCommand = new AsyncRelayCommand(EditSmtpServerAsync, () => !IsBusy);
        EditSmtpPortCommand = new AsyncRelayCommand(EditSmtpPortAsync, () => !IsBusy);
        EditSenderEmailCommand = new AsyncRelayCommand(EditSenderEmailAsync, () => !IsBusy);
        EditSenderPasswordCommand = new AsyncRelayCommand(EditSenderPasswordAsync, () => !IsBusy);
        EditSenderNameCommand = new AsyncRelayCommand(EditSenderNameAsync, () => !IsBusy);
        EditRecipientsCommand = new AsyncRelayCommand(EditRecipientsAsync, () => !IsBusy);

        RefreshLegacyPathProbe();
    }

    private string _legacyConfigPath = string.Empty;

    public string LegacyConfigPath
    {
        get => _legacyConfigPath;
        private set
        {
            if (SetProperty(ref _legacyConfigPath, value))
            {
                OnPropertyChanged(nameof(HasLegacyConfig));
                OnPropertyChanged(nameof(LegacyConfigSummary));
            }
        }
    }

    public bool HasLegacyConfig => !string.IsNullOrEmpty(LegacyConfigPath);

    public string LegacyConfigSummary =>
        HasLegacyConfig
            ? $"Auto-detected at: {LegacyConfigPath}. Use Import to choose this file or another email_config.json."
            : "No email_config.json auto-detected. Use Import to choose your legacy email_config.json file.";

    public string SmtpServer
    {
        get => _smtpServer;
        set
        {
            if (SetProperty(ref _smtpServer, value))
            {
                OnPropertyChanged(nameof(SmtpServerSummary));
            }
        }
    }

    public string SmtpServerSummary =>
        string.IsNullOrWhiteSpace(SmtpServer) ? "Tap to enter SMTP host (e.g. smtp.gmail.com)" : SmtpServer;

    public int SmtpPort
    {
        get => _smtpPort;
        set
        {
            if (SetProperty(ref _smtpPort, value))
            {
                OnPropertyChanged(nameof(SmtpPortSummary));
            }
        }
    }

    public string SmtpPortSummary => SmtpPort > 0 ? SmtpPort.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Tap to enter port";

    public bool EnableSsl
    {
        get => _enableSsl;
        set => SetProperty(ref _enableSsl, value);
    }

    public string SenderEmail
    {
        get => _senderEmail;
        set
        {
            if (SetProperty(ref _senderEmail, value))
            {
                OnPropertyChanged(nameof(SenderEmailSummary));
            }
        }
    }

    public string SenderEmailSummary =>
        string.IsNullOrWhiteSpace(SenderEmail) ? "Tap to enter sender email" : SenderEmail;

    public string SenderPassword
    {
        get => _senderPassword;
        set
        {
            if (SetProperty(ref _senderPassword, value))
            {
                OnPropertyChanged(nameof(SenderPasswordSummary));
            }
        }
    }

    public string SenderPasswordSummary =>
        string.IsNullOrEmpty(SenderPassword) ? "Tap to set password" : new string('•', Math.Min(SenderPassword.Length, 12));

    public string SenderName
    {
        get => _senderName;
        set
        {
            if (SetProperty(ref _senderName, value))
            {
                OnPropertyChanged(nameof(SenderNameSummary));
            }
        }
    }

    public string SenderNameSummary =>
        string.IsNullOrWhiteSpace(SenderName) ? "Tap to enter display name" : SenderName;

    /// <summary>One recipient per line. Saved as a string list under the hood.</summary>
    public string RecipientList
    {
        get => _recipientList;
        set
        {
            if (SetProperty(ref _recipientList, value))
            {
                OnPropertyChanged(nameof(RecipientSummary));
            }
        }
    }

    public string RecipientSummary
    {
        get
        {
            var recipients = ParseRecipients();
            if (recipients.Count == 0)
            {
                return "Tap to enter recipients (comma or new-line separated)";
            }

            return recipients.Count == 1
                ? recipients[0]
                : $"{recipients[0]} (+{recipients.Count - 1} more)";
        }
    }

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand SendTestCommand { get; }

    public IAsyncRelayCommand EditSmtpServerCommand { get; }

    public IAsyncRelayCommand EditSmtpPortCommand { get; }

    public IAsyncRelayCommand EditSenderEmailCommand { get; }

    public IAsyncRelayCommand EditSenderPasswordCommand { get; }

    public IAsyncRelayCommand EditSenderNameCommand { get; }

    public IAsyncRelayCommand EditRecipientsCommand { get; }

    public IAsyncRelayCommand ImportFromV2Command { get; }

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var current = await _config.LoadAsync().ConfigureAwait(true);
            SmtpServer = current.SmtpServer;
            SmtpPort = current.SmtpPort;
            EnableSsl = current.EnableSsl;
            SenderEmail = current.SenderEmail;
            SenderPassword = current.SenderPassword;
            SenderName = current.SenderName;
            RecipientList = string.Join(Environment.NewLine, current.RecipientEmails);
            SetStatus("Loaded.");
        }
        catch (Exception ex)
        {
            SetStatus($"Load failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();
            await _config.SaveAsync(BuildConfig()).ConfigureAwait(true);
            SetStatus("Saved.");
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyBusyChanged();
        }
    }

    private async Task SendTestAsync()
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();
            await _config.SaveAsync(BuildConfig()).ConfigureAwait(true);
            await _sender.SendAsync(
                subject: "Nickeltown POS test email",
                body: "This is a test email from Nickeltown POS v4. If you got this, SMTP is configured correctly.")
                .ConfigureAwait(true);
            SetStatus("Test email sent.");
        }
        catch (Exception ex)
        {
            SetStatus($"Test send failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyBusyChanged();
        }
    }

    private async Task EditSmtpServerAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(SmtpServer ?? string.Empty, "SMTP server host").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        SmtpServer = result.Trim();
    }

    private async Task EditSmtpPortAsync()
    {
        var result = await _inputOverlay.ShowIntegerNumpadAsync(SmtpPort, "SMTP port", 1, 65535).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        SmtpPort = result.Value;
    }

    private async Task EditSenderEmailAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(SenderEmail ?? string.Empty, "Sender email address").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        SenderEmail = result.Trim();
    }

    private async Task EditSenderPasswordAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(SenderPassword ?? string.Empty, "Sender password (or app-password)").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        SenderPassword = result;
    }

    private async Task EditSenderNameAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(SenderName ?? string.Empty, "Sender display name").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        SenderName = result.Trim();
    }

    private async Task ImportFromV2Async()
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();

            var detected = _import.TryFindLegacyConfigPath();
            var path = await _filePicker.PickEmailConfigFileAsync(detected).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                SetStatus("Import cancelled.");
                return;
            }

            var result = await _import.ImportFromPathAsync(path, overwriteExisting: true).ConfigureAwait(true);
            if (result.Imported)
            {
                SetStatus(result.Message ?? "Imported.");
                await LoadAsync().ConfigureAwait(true);
            }
            else
            {
                SetStatus(result.Message ?? "Import skipped.");
            }

            RefreshLegacyPathProbe();
        }
        catch (Exception ex)
        {
            SetStatus($"Import failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyBusyChanged();
        }
    }

    private void RefreshLegacyPathProbe()
    {
        LegacyConfigPath = _import.TryFindLegacyConfigPath() ?? string.Empty;
    }

    private async Task EditRecipientsAsync()
    {
        var current = string.Join(", ", ParseRecipients());
        var result = await _inputOverlay.ShowKeyboardAsync(current, "Recipients (comma or new-line separated)").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        RecipientList = result;
    }

    private System.Collections.Generic.List<string> ParseRecipients() =>
        (_recipientList ?? string.Empty)
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private AppEmailConfig BuildConfig()
    {
        var recipients = ParseRecipients();

        return new AppEmailConfig
        {
            SmtpServer = (SmtpServer ?? string.Empty).Trim(),
            SmtpPort = SmtpPort,
            EnableSsl = EnableSsl,
            SenderEmail = (SenderEmail ?? string.Empty).Trim(),
            SenderPassword = SenderPassword ?? string.Empty,
            SenderName = (SenderName ?? string.Empty).Trim(),
            RecipientEmails = recipients,
        };
    }

    private void NotifyBusyChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
        SendTestCommand.NotifyCanExecuteChanged();
        ImportFromV2Command.NotifyCanExecuteChanged();
        EditSmtpServerCommand.NotifyCanExecuteChanged();
        EditSmtpPortCommand.NotifyCanExecuteChanged();
        EditSenderEmailCommand.NotifyCanExecuteChanged();
        EditSenderPasswordCommand.NotifyCanExecuteChanged();
        EditSenderNameCommand.NotifyCanExecuteChanged();
        EditRecipientsCommand.NotifyCanExecuteChanged();
    }
}
