using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.ViewModels;

public sealed class ForcedPinChangeViewModel : ObservableViewModel
{
    private readonly IUserSessionService _session;
    private readonly IStaffAdminService _staff;
    private readonly IRootNavigationCoordinator _rootNav;

    private string _pinEntry = string.Empty;
    private string _newPin = string.Empty;
    private bool _confirmStep;
    private string _statusMessage = string.Empty;
    private bool _isBusy;

    public ForcedPinChangeViewModel(
        IUserSessionService session,
        IStaffAdminService staff,
        IRootNavigationCoordinator rootNav)
    {
        _session = session;
        _staff = staff;
        _rootNav = rootNav;
        NumpadDigitCommand = new RelayCommand<string>(AppendDigit, _ => !IsBusy);
        NumpadBackspaceCommand = new RelayCommand(Backspace, () => !IsBusy && _pinEntry.Length > 0);
        NumpadClearCommand = new RelayCommand(ClearPin, () => !IsBusy);
    }

    public IRelayCommand<string> NumpadDigitCommand { get; }

    public IRelayCommand NumpadBackspaceCommand { get; }

    public IRelayCommand NumpadClearCommand { get; }

    public bool Dot0Filled => _pinEntry.Length > 0;

    public bool Dot1Filled => _pinEntry.Length > 1;

    public bool Dot2Filled => _pinEntry.Length > 2;

    public bool Dot3Filled => _pinEntry.Length > 3;

    public bool CanEnterDigits => !IsBusy;

    public string StepHint => _confirmStep ? "Enter the same PIN again to confirm." : "Enter your new 4-digit PIN.";

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
            OnPropertyChanged(nameof(CanEnterDigits));
        }
    }

    public void ResetForDisplay()
    {
        _pinEntry = string.Empty;
        _newPin = string.Empty;
        _confirmStep = false;
        StatusMessage = string.Empty;
        NotifyPinDots();
        OnPropertyChanged(nameof(StepHint));
    }

    private void NotifyPinDots()
    {
        OnPropertyChanged(nameof(Dot0Filled));
        OnPropertyChanged(nameof(Dot1Filled));
        OnPropertyChanged(nameof(Dot2Filled));
        OnPropertyChanged(nameof(Dot3Filled));
        NumpadBackspaceCommand.NotifyCanExecuteChanged();
    }

    private void AppendDigit(string? digit)
    {
        if (IsBusy || digit is null || digit.Length != 1 || digit[0] is (< '0' or > '9'))
        {
            return;
        }

        if (_pinEntry.Length >= 4)
        {
            return;
        }

        _pinEntry += digit;
        NotifyPinDots();
        if (!PosBarPinSecurity.IsValidPinFormat(_pinEntry))
        {
            return;
        }

        if (!_confirmStep)
        {
            _newPin = _pinEntry;
            _pinEntry = string.Empty;
            _confirmStep = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(HasStatusMessage));
            OnPropertyChanged(nameof(StepHint));
            NotifyPinDots();
            return;
        }

        _ = SavePinAsync(_newPin, _pinEntry);
    }

    private void Backspace()
    {
        if (_pinEntry.Length == 0)
        {
            return;
        }

        _pinEntry = _pinEntry[..^1];
        NotifyPinDots();
    }

    private void ClearPin()
    {
        _pinEntry = string.Empty;
        NotifyPinDots();
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    private async Task SavePinAsync(string newPin, string confirmPin)
    {
        if (_session.ActiveStaffId is not long staffId)
        {
            _rootNav.NavigateToLogin();
            return;
        }

        if (newPin != confirmPin)
        {
            StatusMessage = "PINs did not match. Start again.";
            ResetForDisplay();
            return;
        }

        IsBusy = true;
        try
        {
            await _staff.CompleteForcedPinChangeAsync(staffId, newPin).ConfigureAwait(true);
            _rootNav.NavigateToMainShell();
        }
        catch (System.Exception ex)
        {
            StatusMessage = PosUserMessage.FromException(ex, "Could not save PIN. Try again.");
            ResetForDisplay();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
