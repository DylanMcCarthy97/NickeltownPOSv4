using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Updates;
using NickeltownPOSV4.Views;

namespace NickeltownPOSV4.ViewModels.Settings;

public sealed class UpdateConfigViewModel : ObservableViewModel
{
    private readonly IAppUpdateConfigService _config;
    private readonly IAppUpdateService _updates;
    private readonly INavigationService _navigation;

    private bool _checkOnStartup = true;
    private bool _autoInstall;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    private Func<XamlRoot?>? _xamlRootProvider;

    public UpdateConfigViewModel(
        IAppUpdateConfigService config,
        IAppUpdateService updates,
        INavigationService navigation)
    {
        _config = config;
        _updates = updates;
        _navigation = navigation;
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CheckNowCommand = new AsyncRelayCommand(CheckNowAsync, () => !IsBusy);
        BackCommand = new RelayCommand(GoBack);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand CheckNowCommand { get; }

    public IRelayCommand BackCommand { get; }

    public string CurrentBuildText => $"This version: {AppVersionInfo.CurrentVersionString}";

    public string FeedSourceSummary => "New versions arrive automatically. Tap Check for update if you want to look now.";

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
                CheckOnStartup = CheckOnStartup,
                AutoInstall = AutoInstall,
            }).ConfigureAwait(true);
            StatusMessage = "Saved.";
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
                StatusMessage = $"Version {check.Manifest!.Version} is ready to install.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(check.ErrorMessage))
            {
                StatusMessage = PosUserMessage.IsOperatorSafe(check.ErrorMessage)
                    ? check.ErrorMessage
                    : "We couldn't check for updates. Try again.";
                return;
            }

            StatusMessage = "You're already on the latest version.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void GoBack()
    {
        if (!_navigation.TryGoBack())
        {
            _navigation.Navigate(typeof(AdminHomePage));
        }
    }
}
