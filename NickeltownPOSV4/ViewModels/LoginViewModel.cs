using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Themes;

namespace NickeltownPOSV4.ViewModels;

public sealed class LoginViewModel : ObservableViewModel
{
    private readonly IAuthenticationService _auth;

    private readonly IUserSessionService _session;

    private readonly IRootNavigationCoordinator _rootNav;

    private readonly IPosThemeService _themes;

    private string _pinEntry = string.Empty;

    private string _statusMessage = string.Empty;

    private bool _isBusy;

    private int _pinFailScheduleId;

    public LoginViewModel(
        IAuthenticationService auth,
        IUserSessionService session,
        IRootNavigationCoordinator rootNav,
        IPosThemeService themes)
    {
        _auth = auth;
        _session = session;
        _rootNav = rootNav;
        _themes = themes;

        NumpadDigitCommand = new RelayCommand<string>(AppendDigit, _ => !IsBusy);
        NumpadBackspaceCommand = new RelayCommand(Backspace, () => !IsBusy && _pinEntry.Length > 0);
        NumpadClearCommand = new RelayCommand(ClearPin, () => !IsBusy);
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => IsReadyToSubmit);
    }

    public IRelayCommand<string> NumpadDigitCommand { get; }

    public IRelayCommand NumpadBackspaceCommand { get; }

    public IRelayCommand NumpadClearCommand { get; }

    public IAsyncRelayCommand SubmitCommand { get; }

    public bool Dot0Filled => _pinEntry.Length > 0;

    public bool Dot1Filled => _pinEntry.Length > 1;

    public bool Dot2Filled => _pinEntry.Length > 2;

    public bool Dot3Filled => _pinEntry.Length > 3;

    public bool IsReadyToSubmit => !IsBusy && PosBarPinSecurity.IsValidPinFormat(_pinEntry);

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (!SetProperty(ref _statusMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
            {
                return;
            }

            NumpadDigitCommand.NotifyCanExecuteChanged();
            NumpadBackspaceCommand.NotifyCanExecuteChanged();
            NumpadClearCommand.NotifyCanExecuteChanged();
            SubmitCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(IsReadyToSubmit));
        }
    }

    public bool CanEnterDigits => !IsBusy;

    public void ResetForDisplay()
    {
        ClearPin();
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    private void NotifyPinDots()
    {
        OnPropertyChanged(nameof(Dot0Filled));
        OnPropertyChanged(nameof(Dot1Filled));
        OnPropertyChanged(nameof(Dot2Filled));
        OnPropertyChanged(nameof(Dot3Filled));
        OnPropertyChanged(nameof(IsReadyToSubmit));
        NumpadBackspaceCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void AppendDigit(string? digit)
    {
        if (IsBusy || digit is null || digit.Length != 1)
        {
            return;
        }

        if (digit[0] is (< '0' or > '9'))
        {
            return;
        }

        _pinFailScheduleId++;
        if (HasStatusMessage)
        {
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));
            _pinEntry = string.Empty;
            NotifyPinDots();
        }

        if (_pinEntry.Length >= 4)
        {
            return;
        }

        _pinEntry += digit;
        NotifyPinDots();
        SubmitCommand.NotifyCanExecuteChanged();
        if (PosBarPinSecurity.IsValidPinFormat(_pinEntry))
        {
            _ = SubmitCommand.ExecuteAsync(null);
        }
    }

    private void Backspace()
    {
        _pinFailScheduleId++;
        if (_pinEntry.Length == 0)
        {
            return;
        }

        _pinEntry = _pinEntry[..^1];
        NotifyPinDots();
        SubmitCommand.NotifyCanExecuteChanged();
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    private void ClearPin()
    {
        _pinFailScheduleId++;
        _pinEntry = string.Empty;
        NotifyPinDots();
        SubmitCommand.NotifyCanExecuteChanged();
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    private void ClearPinDigitsOnly()
    {
        _pinEntry = string.Empty;
        NotifyPinDots();
        SubmitCommand.NotifyCanExecuteChanged();
    }

    private void SchedulePinClearAfterFailedAuth()
    {
        var myId = ++_pinFailScheduleId;
        _ = Task.Delay(1200).ContinueWith(
            _ => TcxLayoutDiagnostics.TryEnqueueNormal(() =>
            {
                if (myId != _pinFailScheduleId)
                {
                    return;
                }

                ClearPinDigitsOnly();
            }),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private async Task SubmitAsync()
    {
        if (!IsReadyToSubmit)
        {
            return;
        }

        _pinFailScheduleId++;
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var r = await _auth.AuthenticateByPinAsync(_pinEntry).ConfigureAwait(true);
            if (!r.Ok)
            {
                StatusMessage = r.ErrorMessage ?? "Sign-in failed.";
                SchedulePinClearAfterFailedAuth();
                return;
            }

            _session.SetSignedIn(r.StaffPk, r.LegacyId, r.DisplayName, r.Role);
            ApplyThemeFromAuth(r.UiTheme);
            ClearPin();
            if (r.RequiresPinChange)
            {
                _rootNav.NavigateToForcedPinChange();
            }
            else
            {
                _rootNav.NavigateToMainShell();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyThemeFromAuth(string? uiTheme)
    {
        if (string.IsNullOrWhiteSpace(uiTheme))
        {
            _themes.Apply(UiThemeId.Light);
            return;
        }

        if (Enum.TryParse<UiThemeId>(uiTheme.Trim(), ignoreCase: true, out var id))
        {
            _themes.Apply(id);
        }
        else
        {
            _themes.Apply(UiThemeId.Light);
        }
    }
}
