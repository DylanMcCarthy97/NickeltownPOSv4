using System;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Membership;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Membership;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipSettingsViewModel : MembershipSubViewModelBase
{
    private readonly IMembershipSettingsService _settings;
    private readonly IInputOverlayService _inputOverlay;

    private string _membershipYearLabel = "2026/2027";
    private string _membershipYearStartText = "1 Jul 2026";
    private string _membershipYearEndText = "30 Jun 2027";
    private string _joiningFeeFullText = "$65.00";
    private string _joiningFeeHalfText = "$32.50";
    private string _renewalFeeText = "$0.00";
    private string _reminderDaysText = "30";
    private string _committeeEmail = string.Empty;
    private string _clubName = string.Empty;
    private string _clubAbn = string.Empty;
    private string _clubPoBox = string.Empty;
    private string _clubPhone = string.Empty;
    private string _clubEmail = string.Empty;
    private string _logoPath = string.Empty;

    private DateOnly _membershipYearStart = new(2026, 7, 1);
    private DateOnly _membershipYearEnd = new(2027, 6, 30);
    private decimal _joiningFeeFull = 65.00m;
    private decimal _joiningFeeHalf = 32.50m;
    private decimal _renewalFee;
    private int _reminderDays = 30;

    public MembershipSettingsViewModel(
        INavigationService navigation,
        IMembershipSettingsService settings,
        IInputOverlayService inputOverlay)
        : base(navigation)
    {
        _settings = settings;
        _inputOverlay = inputOverlay;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);

        EditMembershipYearLabelCommand = new AsyncRelayCommand(EditMembershipYearLabelAsync, () => !IsBusy);
        EditMembershipYearStartCommand = new AsyncRelayCommand(EditMembershipYearStartAsync, () => !IsBusy);
        EditMembershipYearEndCommand = new AsyncRelayCommand(EditMembershipYearEndAsync, () => !IsBusy);
        EditJoiningFeeFullCommand = new AsyncRelayCommand(EditJoiningFeeFullAsync, () => !IsBusy);
        EditJoiningFeeHalfCommand = new AsyncRelayCommand(EditJoiningFeeHalfAsync, () => !IsBusy);
        EditRenewalFeeCommand = new AsyncRelayCommand(EditRenewalFeeAsync, () => !IsBusy);
        EditReminderDaysCommand = new AsyncRelayCommand(EditReminderDaysAsync, () => !IsBusy);
        EditCommitteeEmailCommand = new AsyncRelayCommand(EditCommitteeEmailAsync, () => !IsBusy);
        EditClubNameCommand = new AsyncRelayCommand(EditClubNameAsync, () => !IsBusy);
        EditClubAbnCommand = new AsyncRelayCommand(EditClubAbnAsync, () => !IsBusy);
        EditClubPoBoxCommand = new AsyncRelayCommand(EditClubPoBoxAsync, () => !IsBusy);
        EditClubPhoneCommand = new AsyncRelayCommand(EditClubPhoneAsync, () => !IsBusy);
        EditClubEmailCommand = new AsyncRelayCommand(EditClubEmailAsync, () => !IsBusy);
        EditLogoPathCommand = new AsyncRelayCommand(EditLogoPathAsync, () => !IsBusy);
    }

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand EditMembershipYearLabelCommand { get; }

    public IAsyncRelayCommand EditMembershipYearStartCommand { get; }

    public IAsyncRelayCommand EditMembershipYearEndCommand { get; }

    public IAsyncRelayCommand EditJoiningFeeFullCommand { get; }

    public IAsyncRelayCommand EditJoiningFeeHalfCommand { get; }

    public IAsyncRelayCommand EditRenewalFeeCommand { get; }

    public IAsyncRelayCommand EditReminderDaysCommand { get; }

    public IAsyncRelayCommand EditCommitteeEmailCommand { get; }

    public IAsyncRelayCommand EditClubNameCommand { get; }

    public IAsyncRelayCommand EditClubAbnCommand { get; }

    public IAsyncRelayCommand EditClubPoBoxCommand { get; }

    public IAsyncRelayCommand EditClubPhoneCommand { get; }

    public IAsyncRelayCommand EditClubEmailCommand { get; }

    public IAsyncRelayCommand EditLogoPathCommand { get; }

    public string MembershipYearLabel
    {
        get => _membershipYearLabel;
        private set => SetProperty(ref _membershipYearLabel, value);
    }

    public string MembershipYearStartText
    {
        get => _membershipYearStartText;
        private set => SetProperty(ref _membershipYearStartText, value);
    }

    public string MembershipYearEndText
    {
        get => _membershipYearEndText;
        private set => SetProperty(ref _membershipYearEndText, value);
    }

    public string JoiningFeeFullText
    {
        get => _joiningFeeFullText;
        private set => SetProperty(ref _joiningFeeFullText, value);
    }

    public string JoiningFeeHalfText
    {
        get => _joiningFeeHalfText;
        private set => SetProperty(ref _joiningFeeHalfText, value);
    }

    public string RenewalFeeText
    {
        get => _renewalFeeText;
        private set => SetProperty(ref _renewalFeeText, value);
    }

    public string ReminderDaysText
    {
        get => _reminderDaysText;
        private set => SetProperty(ref _reminderDaysText, value);
    }

    public string CommitteeEmailSummary =>
        string.IsNullOrWhiteSpace(_committeeEmail) ? "Tap to set committee email" : _committeeEmail;

    public string ClubNameSummary =>
        string.IsNullOrWhiteSpace(_clubName) ? "Tap to set club name" : _clubName;

    public string ClubAbnSummary =>
        string.IsNullOrWhiteSpace(_clubAbn) ? "Tap to set ABN" : _clubAbn;

    public string ClubPoBoxSummary =>
        string.IsNullOrWhiteSpace(_clubPoBox) ? "Tap to set PO Box" : _clubPoBox;

    public string ClubPhoneSummary =>
        string.IsNullOrWhiteSpace(_clubPhone) ? "Tap to set phone" : _clubPhone;

    public string ClubEmailSummary =>
        string.IsNullOrWhiteSpace(_clubEmail) ? "Tap to set club email" : _clubEmail;

    public string LogoPathSummary =>
        string.IsNullOrWhiteSpace(_logoPath) ? "Tap to set logo path (optional)" : _logoPath;

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();
            var current = await _settings.LoadAsync().ConfigureAwait(true);
            ApplySettings(current);
            SetStatus("Loaded membership settings.");
        }
        catch (Exception ex)
        {
            SetStatus($"Load failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyBusyChanged();
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();
            await _settings.SaveAsync(new MembershipSettings
            {
                MembershipYearLabel = _membershipYearLabel.Trim(),
                MembershipYearStart = _membershipYearStart,
                MembershipYearEnd = _membershipYearEnd,
                JoiningFeeFull = _joiningFeeFull,
                JoiningFeeHalf = _joiningFeeHalf,
                RenewalFee = _renewalFee,
                ReminderDaysBeforeExpiry = _reminderDays,
                CommitteeEmail = _committeeEmail.Trim(),
                ClubName = _clubName.Trim(),
                ClubAbn = _clubAbn.Trim(),
                ClubPoBox = _clubPoBox.Trim(),
                ClubPhone = _clubPhone.Trim(),
                ClubEmail = _clubEmail.Trim(),
                LogoPath = string.IsNullOrWhiteSpace(_logoPath) ? null : _logoPath.Trim(),
            }).ConfigureAwait(true);
            SetStatus("Membership settings saved.");
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

    private void ApplySettings(MembershipSettings current)
    {
        _membershipYearStart = current.MembershipYearStart;
        _membershipYearEnd = current.MembershipYearEnd;
        _joiningFeeFull = current.JoiningFeeFull;
        _joiningFeeHalf = current.JoiningFeeHalf;
        _renewalFee = current.RenewalFee;
        _reminderDays = current.ReminderDaysBeforeExpiry;
        _committeeEmail = current.CommitteeEmail;
        _clubName = current.ClubName;
        _clubAbn = current.ClubAbn;
        _clubPoBox = current.ClubPoBox;
        _clubPhone = current.ClubPhone;
        _clubEmail = current.ClubEmail;
        _logoPath = current.LogoPath ?? string.Empty;

        MembershipYearLabel = current.MembershipYearLabel;
        MembershipYearStartText = current.MembershipYearStart.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        MembershipYearEndText = current.MembershipYearEnd.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        JoiningFeeFullText = current.JoiningFeeFull.ToString("C2", CultureInfo.GetCultureInfo("en-AU"));
        JoiningFeeHalfText = current.JoiningFeeHalf.ToString("C2", CultureInfo.GetCultureInfo("en-AU"));
        RenewalFeeText = current.RenewalFee.ToString("C2", CultureInfo.GetCultureInfo("en-AU"));
        ReminderDaysText = current.ReminderDaysBeforeExpiry.ToString(CultureInfo.InvariantCulture);

        OnPropertyChanged(nameof(CommitteeEmailSummary));
        OnPropertyChanged(nameof(ClubNameSummary));
        OnPropertyChanged(nameof(ClubAbnSummary));
        OnPropertyChanged(nameof(ClubPoBoxSummary));
        OnPropertyChanged(nameof(ClubPhoneSummary));
        OnPropertyChanged(nameof(ClubEmailSummary));
        OnPropertyChanged(nameof(LogoPathSummary));
    }

    private async Task EditMembershipYearLabelAsync()
    {
        var result = await _inputOverlay.ShowKeyboardAsync(_membershipYearLabel, "Membership year label").ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        MembershipYearLabel = result.Trim();
    }

    private async Task EditMembershipYearStartAsync() => await EditDateAsync(
        _membershipYearStart,
        "Membership year start",
        d =>
        {
            _membershipYearStart = d;
            MembershipYearStartText = d.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }).ConfigureAwait(true);

    private async Task EditMembershipYearEndAsync() => await EditDateAsync(
        _membershipYearEnd,
        "Membership year end",
        d =>
        {
            _membershipYearEnd = d;
            MembershipYearEndText = d.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        }).ConfigureAwait(true);

    private async Task EditJoiningFeeFullAsync() => await EditMoneyAsync(
        _joiningFeeFull,
        "Joining fee (Jul–Dec)",
        v =>
        {
            _joiningFeeFull = v;
            JoiningFeeFullText = v.ToString("C2", CultureInfo.GetCultureInfo("en-AU"));
        }).ConfigureAwait(true);

    private async Task EditJoiningFeeHalfAsync() => await EditMoneyAsync(
        _joiningFeeHalf,
        "Joining fee (Jan–Jun)",
        v =>
        {
            _joiningFeeHalf = v;
            JoiningFeeHalfText = v.ToString("C2", CultureInfo.GetCultureInfo("en-AU"));
        }).ConfigureAwait(true);

    private async Task EditRenewalFeeAsync() => await EditMoneyAsync(
        _renewalFee,
        "Renewal fee",
        v =>
        {
            _renewalFee = v;
            RenewalFeeText = v.ToString("C2", CultureInfo.GetCultureInfo("en-AU"));
        }).ConfigureAwait(true);

    private async Task EditReminderDaysAsync()
    {
        var result = await _inputOverlay.ShowNumpadAsync(_reminderDays, "Reminder days before expiry", false, default).ConfigureAwait(true);
        if (!result.HasValue)
        {
            return;
        }

        _reminderDays = (int)Math.Clamp(decimal.Truncate(result.Value), 1m, 365m);
        ReminderDaysText = _reminderDays.ToString(CultureInfo.InvariantCulture);
    }

    private async Task EditCommitteeEmailAsync() => await EditTextAsync(
        _committeeEmail,
        "Committee email",
        v =>
        {
            _committeeEmail = v;
            OnPropertyChanged(nameof(CommitteeEmailSummary));
        }).ConfigureAwait(true);

    private async Task EditClubNameAsync() => await EditTextAsync(
        _clubName,
        "Club name",
        v =>
        {
            _clubName = v;
            OnPropertyChanged(nameof(ClubNameSummary));
        }).ConfigureAwait(true);

    private async Task EditClubAbnAsync() => await EditTextAsync(
        _clubAbn,
        "Club ABN",
        v =>
        {
            _clubAbn = v;
            OnPropertyChanged(nameof(ClubAbnSummary));
        }).ConfigureAwait(true);

    private async Task EditClubPoBoxAsync() => await EditTextAsync(
        _clubPoBox,
        "Club PO Box",
        v =>
        {
            _clubPoBox = v;
            OnPropertyChanged(nameof(ClubPoBoxSummary));
        }).ConfigureAwait(true);

    private async Task EditClubPhoneAsync() => await EditTextAsync(
        _clubPhone,
        "Club phone",
        v =>
        {
            _clubPhone = v;
            OnPropertyChanged(nameof(ClubPhoneSummary));
        }).ConfigureAwait(true);

    private async Task EditClubEmailAsync() => await EditTextAsync(
        _clubEmail,
        "Club email",
        v =>
        {
            _clubEmail = v;
            OnPropertyChanged(nameof(ClubEmailSummary));
        }).ConfigureAwait(true);

    private async Task EditLogoPathAsync() => await EditTextAsync(
        _logoPath,
        "Logo file path",
        v =>
        {
            _logoPath = v;
            OnPropertyChanged(nameof(LogoPathSummary));
        }).ConfigureAwait(true);

    private async Task EditMoneyAsync(decimal current, string title, Action<decimal> apply)
    {
        var result = await _inputOverlay.ShowNumpadAsync(current, title, false, default).ConfigureAwait(true);
        if (!result.HasValue)
        {
            return;
        }

        apply(decimal.Round(Math.Max(0m, result.Value), 2, MidpointRounding.AwayFromZero));
    }

    private async Task EditTextAsync(string current, string title, Action<string> apply)
    {
        var result = await _inputOverlay.ShowKeyboardAsync(current, title).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        apply(result.Trim());
    }

    private async Task EditDateAsync(DateOnly current, string title, Action<DateOnly> apply)
    {
        var initial = current.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var result = await _inputOverlay.ShowKeyboardAsync(initial, title).ConfigureAwait(true);
        if (result is null)
        {
            return;
        }

        if (!TryParseMembershipDate(result, out var parsed))
        {
            SetStatus("Enter a valid date (e.g. 1/7/2026 or 01/07/2026).");
            return;
        }

        apply(parsed);
    }

    private static bool TryParseMembershipDate(string value, out DateOnly date)
    {
        date = default;
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        string[] formats =
        [
            "d/M/yyyy",
            "dd/MM/yyyy",
            "d/M/yy",
            "dd/MM/yy",
            "d MMM yyyy",
            "dd MMM yyyy",
            "yyyy-MM-dd",
        ];

        if (DateOnly.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        if (DateOnly.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private void NotifyBusyChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
        EditMembershipYearLabelCommand.NotifyCanExecuteChanged();
        EditMembershipYearStartCommand.NotifyCanExecuteChanged();
        EditMembershipYearEndCommand.NotifyCanExecuteChanged();
        EditJoiningFeeFullCommand.NotifyCanExecuteChanged();
        EditJoiningFeeHalfCommand.NotifyCanExecuteChanged();
        EditRenewalFeeCommand.NotifyCanExecuteChanged();
        EditReminderDaysCommand.NotifyCanExecuteChanged();
        EditCommitteeEmailCommand.NotifyCanExecuteChanged();
        EditClubNameCommand.NotifyCanExecuteChanged();
        EditClubAbnCommand.NotifyCanExecuteChanged();
        EditClubPoBoxCommand.NotifyCanExecuteChanged();
        EditClubPhoneCommand.NotifyCanExecuteChanged();
        EditClubEmailCommand.NotifyCanExecuteChanged();
        EditLogoPathCommand.NotifyCanExecuteChanged();
    }
}
