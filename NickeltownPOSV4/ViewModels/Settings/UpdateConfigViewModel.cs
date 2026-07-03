using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Updates;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class UpdateConfigViewModel : ObservableViewModel
{
    private readonly IAppUpdateConfigService _config;
    private readonly IAppUpdateService _updates;

    private string _feedBaseUrl = string.Empty;
    private bool _checkOnStartup = true;
    private bool _autoInstall;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    private Func<XamlRoot?>? _xamlRootProvider;

    public UpdateConfigViewModel(IAppUpdateConfigService config, IAppUpdateService updates)
    {
        _config = config;
        _updates = updates;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CheckNowCommand = new AsyncRelayCommand(CheckNowAsync, () => !IsBusy);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand CheckNowCommand { get; }

    public string CurrentVersionText => $"Installed: {AppVersionInfo.CurrentVersionString}";

    public string FeedBaseUrl
    {
        get => _feedBaseUrl;
        set => SetProperty(ref _feedBaseUrl, value);
    }

    public bool CheckOnStartup
    {
        get => _checkOnStartup;
        set => SetProperty(ref _checkOnStartup, value);
    }

    public bool AutoInstall
    {
        get => _autoInstall;
        set => SetProperty(ref _autoInstall, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                CheckNowCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public void AttachXamlRoot(Func<XamlRoot?> provider) => _xamlRootProvider = provider;

    public async Task LoadAsync()
    {
        var cfg = await _config.LoadAsync().ConfigureAwait(true);
        FeedBaseUrl = cfg.FeedBaseUrl ?? string.Empty;
        CheckOnStartup = cfg.CheckOnStartup;
        AutoInstall = cfg.AutoInstall;
        StatusMessage = string.Empty;
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            await _config.SaveAsync(new AppUpdateConfig
            {
                FeedBaseUrl = FeedBaseUrl.Trim(),
                CheckOnStartup = CheckOnStartup,
                AutoInstall = AutoInstall,
            }).ConfigureAwait(true);
            StatusMessage = "Update settings saved.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckNowAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            if (_xamlRootProvider?.Invoke() is XamlRoot root)
            {
                var installed = await AppUpdateUiHelper.TryPromptAndInstallAsync(root, AutoInstall).ConfigureAwait(true);
                if (installed)
                {
                    return;
                }
            }

            var check = await _updates.CheckForUpdateAsync().ConfigureAwait(true);
            if (check.UpdateAvailable)
            {
                StatusMessage = $"Update {check.Manifest!.Version} is available.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(check.ErrorMessage))
            {
                StatusMessage = check.ErrorMessage;
                return;
            }

            StatusMessage = "You are on the latest version.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
