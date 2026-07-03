using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Models.Membership;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Membership;
using NickeltownPOSV4.Views.Membership;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipPaperApplicationViewModel : MembershipSubViewModelBase
{
    private readonly IMembershipApplicationService _applications;
    private readonly IMembershipFormContentService _formContent;
    private readonly IInputOverlayService _inputOverlay;

    private long _applicationId;
    private ApplicationStatus _status = ApplicationStatus.Draft;
    private string _pageTitle = "New paper application";
    private string _applicationNumber = string.Empty;

    private string _surname = string.Empty;
    private string _givenNames = string.Empty;
    private string _childrenUnder18 = string.Empty;
    private string _address = string.Empty;
    private string _postCode = string.Empty;
    private DateOnly? _dateOfBirth;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _mobile = string.Empty;
    private string _additionalComments = string.Empty;

    private bool _paperDeclarationSigned;
    private string _signatureDateText = string.Empty;
    private DateTimeOffset? _signedAt;

    private string _declarationText = string.Empty;
    private string _feeStructureIntro = string.Empty;
    private string _fullYearFeeLine = string.Empty;
    private string _halfYearFeeLine = string.Empty;
    private string _applicableFeeLine = string.Empty;
    private string _selectedFeeText = string.Empty;
    private MembershipFeeType _selectedFeeType = MembershipFeeType.FullYear;
    private decimal _selectedFeeAmount;

    private decimal _fullYearAmount;
    private decimal _halfYearAmount;

    private bool _hasNoVehicle;

    private string _sectionApplicantDetails = "Applicant details";
    private string _sectionVehicleDetails = "Vehicle details";
    private string _fieldAdditionalCommentsLabel = "Additional comments";

    public MembershipPaperApplicationViewModel(
        INavigationService navigation,
        IMembershipApplicationService applications,
        IMembershipFormContentService formContent,
        IInputOverlayService inputOverlay)
        : base(navigation)
    {
        _applications = applications;
        _formContent = formContent;
        _inputOverlay = inputOverlay;

        Vehicles = new ObservableCollection<MembershipApplicationVehicleEntryViewModel>();

        SaveDraftCommand = new AsyncRelayCommand(() => SaveAsync(submit: false), () => !IsBusy);
        SubmitCommand = new AsyncRelayCommand(() => SaveAsync(submit: true), () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => !IsBusy);
        AddVehicleCommand = new RelayCommand(AddVehicle, () => !IsBusy && !HasNoVehicle);
        SelectFullYearFeeCommand = new RelayCommand(SelectFullYearFee, () => !IsBusy);
        SelectHalfYearFeeCommand = new RelayCommand(SelectHalfYearFee, () => !IsBusy);
        OverrideFeeCommand = new AsyncRelayCommand(OverrideFeeAsync, () => !IsBusy);

        EditSurnameCommand = new AsyncRelayCommand(() => EditTextAsync(_surname, "Surname", v => Surname = v));
        EditGivenNamesCommand = new AsyncRelayCommand(() => EditTextAsync(_givenNames, "Given names", v => GivenNames = v));
        EditChildrenUnder18Command = new AsyncRelayCommand(() => EditTextAsync(_childrenUnder18, "Children under 18", v => ChildrenUnder18 = v));
        EditAddressCommand = new AsyncRelayCommand(() => EditTextAsync(_address, "Address", v => Address = v));
        EditPostCodeCommand = new AsyncRelayCommand(() => EditDigitStringAsync(_postCode, "Post code", 4, v => PostCode = v));
        EditDateOfBirthCommand = new AsyncRelayCommand(EditDateOfBirthAsync);
        EditEmailCommand = new AsyncRelayCommand(() => EditTextAsync(_email, "Email address", v => Email = v));
        EditPhoneCommand = new AsyncRelayCommand(() => EditDigitStringAsync(_phone, "Phone number", 15, v => Phone = v));
        EditMobileCommand = new AsyncRelayCommand(() => EditDigitStringAsync(_mobile, "Mobile", 15, v => Mobile = v));
        EditAdditionalCommentsCommand = new AsyncRelayCommand(() => EditTextAsync(_additionalComments, "Additional comments", v => AdditionalComments = v));
        EditSignatureDateCommand = new AsyncRelayCommand(EditSignatureDateAsync);
    }

    public ObservableCollection<MembershipApplicationVehicleEntryViewModel> Vehicles { get; }

    public IAsyncRelayCommand SaveDraftCommand { get; }
    public IAsyncRelayCommand SubmitCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand AddVehicleCommand { get; }
    public IRelayCommand SelectFullYearFeeCommand { get; }
    public IRelayCommand SelectHalfYearFeeCommand { get; }
    public IAsyncRelayCommand OverrideFeeCommand { get; }

    public IAsyncRelayCommand EditSurnameCommand { get; }
    public IAsyncRelayCommand EditGivenNamesCommand { get; }
    public IAsyncRelayCommand EditChildrenUnder18Command { get; }
    public IAsyncRelayCommand EditAddressCommand { get; }
    public IAsyncRelayCommand EditPostCodeCommand { get; }
    public IAsyncRelayCommand EditDateOfBirthCommand { get; }
    public IAsyncRelayCommand EditEmailCommand { get; }
    public IAsyncRelayCommand EditPhoneCommand { get; }
    public IAsyncRelayCommand EditMobileCommand { get; }
    public IAsyncRelayCommand EditAdditionalCommentsCommand { get; }
    public IAsyncRelayCommand EditSignatureDateCommand { get; }

    public string PageTitle
    {
        get => _pageTitle;
        private set => SetProperty(ref _pageTitle, value);
    }

    public string ApplicationNumber
    {
        get => _applicationNumber;
        private set => SetProperty(ref _applicationNumber, value);
    }

    public string Surname
    {
        get => _surname;
        set
        {
            if (SetProperty(ref _surname, value))
            {
                OnPropertyChanged(nameof(SurnameSummary));
            }
        }
    }

    public string GivenNames
    {
        get => _givenNames;
        set
        {
            if (SetProperty(ref _givenNames, value))
            {
                OnPropertyChanged(nameof(GivenNamesSummary));
            }
        }
    }

    public string ChildrenUnder18
    {
        get => _childrenUnder18;
        set
        {
            if (SetProperty(ref _childrenUnder18, value))
            {
                OnPropertyChanged(nameof(ChildrenUnder18Summary));
            }
        }
    }

    public string Address
    {
        get => _address;
        set
        {
            if (SetProperty(ref _address, value))
            {
                OnPropertyChanged(nameof(AddressSummary));
            }
        }
    }

    public string PostCode
    {
        get => _postCode;
        set
        {
            if (SetProperty(ref _postCode, value))
            {
                OnPropertyChanged(nameof(PostCodeSummary));
            }
        }
    }

    public bool HasNoVehicle
    {
        get => _hasNoVehicle;
        set
        {
            if (SetProperty(ref _hasNoVehicle, value))
            {
                OnHasNoVehicleChanged();
                OnPropertyChanged(nameof(VehicleSectionVisible));
                AddVehicleCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool VehicleSectionVisible => !HasNoVehicle;

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
            {
                OnPropertyChanged(nameof(EmailSummary));
            }
        }
    }

    public string Phone
    {
        get => _phone;
        set
        {
            if (SetProperty(ref _phone, value))
            {
                OnPropertyChanged(nameof(PhoneSummary));
            }
        }
    }

    public string Mobile
    {
        get => _mobile;
        set
        {
            if (SetProperty(ref _mobile, value))
            {
                OnPropertyChanged(nameof(MobileSummary));
            }
        }
    }

    public string AdditionalComments
    {
        get => _additionalComments;
        set
        {
            if (SetProperty(ref _additionalComments, value))
            {
                OnPropertyChanged(nameof(AdditionalCommentsSummary));
            }
        }
    }

    public bool PaperDeclarationSigned
    {
        get => _paperDeclarationSigned;
        set => SetProperty(ref _paperDeclarationSigned, value);
    }

    public string SignatureDateText
    {
        get => _signatureDateText;
        private set => SetProperty(ref _signatureDateText, value);
    }

    public string DeclarationText
    {
        get => _declarationText;
        private set => SetProperty(ref _declarationText, value);
    }

    public string FeeStructureIntro
    {
        get => _feeStructureIntro;
        private set => SetProperty(ref _feeStructureIntro, value);
    }

    public string FullYearFeeLine
    {
        get => _fullYearFeeLine;
        private set => SetProperty(ref _fullYearFeeLine, value);
    }

    public string HalfYearFeeLine
    {
        get => _halfYearFeeLine;
        private set => SetProperty(ref _halfYearFeeLine, value);
    }

    public string ApplicableFeeLine
    {
        get => _applicableFeeLine;
        private set => SetProperty(ref _applicableFeeLine, value);
    }

    public string SelectedFeeText
    {
        get => _selectedFeeText;
        private set => SetProperty(ref _selectedFeeText, value);
    }

    public string SectionApplicantDetails
    {
        get => _sectionApplicantDetails;
        private set => SetProperty(ref _sectionApplicantDetails, value);
    }

    public string SectionVehicleDetails
    {
        get => _sectionVehicleDetails;
        private set => SetProperty(ref _sectionVehicleDetails, value);
    }

    public string FieldAdditionalCommentsLabel
    {
        get => _fieldAdditionalCommentsLabel;
        private set => SetProperty(ref _fieldAdditionalCommentsLabel, value);
    }

    public string SurnameSummary => DisplayOrPlaceholder(Surname);
    public string GivenNamesSummary => DisplayOrPlaceholder(GivenNames);
    public string ChildrenUnder18Summary => DisplayOrPlaceholder(ChildrenUnder18);
    public string AddressSummary => DisplayOrPlaceholder(Address);
    public string PostCodeSummary => DisplayOrPlaceholder(PostCode);
    public string DateOfBirthSummary => _dateOfBirth.HasValue
        ? MembershipDateFormats.FormatAustralianDate(_dateOfBirth.Value)
        : "Tap to enter (dd/MM/yyyy)";
    public string EmailSummary => DisplayOrPlaceholder(Email);
    public string PhoneSummary => DisplayOrPlaceholder(Phone);
    public string MobileSummary => DisplayOrPlaceholder(Mobile);
    public string AdditionalCommentsSummary => DisplayOrPlaceholder(AdditionalComments);
    public string SignatureDateSummary => string.IsNullOrWhiteSpace(SignatureDateText) ? "Tap to enter (dd/MM/yyyy)" : SignatureDateText;

    public async Task LoadAsync(long applicationId)
    {
        try
        {
            IsBusy = true;
            await LoadFormContentAsync().ConfigureAwait(true);
            await LoadFeeDisplayAsync().ConfigureAwait(true);

            MembershipApplicationDetail detail;
            if (applicationId <= 0)
            {
                detail = await _applications.CreateNewPaperApplicationAsync().ConfigureAwait(true);
            }
            else
            {
                detail = await _applications.GetAsync(applicationId).ConfigureAwait(true)
                    ?? throw new InvalidOperationException("Application not found.");
            }

            ApplyDetail(detail);
            SetStatus(applicationId <= 0 ? "Enter applicant details and save as draft or submit for review." : "Application loaded.");
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

    private async Task SaveAsync(bool submit)
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();
            var detail = BuildDetail();
            if (submit)
            {
                var validation = _applications.ValidateForSubmit(detail);
                if (!validation.IsValid)
                {
                    SetStatus(string.Join(" ", validation.Errors));
                    return;
                }

                detail = await _applications.SubmitForReviewAsync(detail).ConfigureAwait(true);
                Navigate(typeof(MembershipApplicationsPage), "Application submitted for review.");
                return;
            }

            await _applications.SaveDraftAsync(detail).ConfigureAwait(true);
            Navigate(typeof(MembershipApplicationsPage), "Draft saved.");
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

    private void Cancel() => Navigate(typeof(MembershipApplicationsPage));

    private void AddVehicle()
    {
        Vehicles.Add(CreateVehicleViewModel(Vehicles.Count));
        RefreshVehicleTitles();
    }

    private void RemoveVehicle(MembershipApplicationVehicleEntryViewModel vehicle)
    {
        Vehicles.Remove(vehicle);
        if (Vehicles.Count == 0)
        {
            Vehicles.Add(CreateVehicleViewModel(0));
        }

        RefreshVehicleTitles();
    }

    private MembershipApplicationVehicleEntryViewModel CreateVehicleViewModel(int index) =>
        new(_inputOverlay, RemoveVehicle, index);

    private void RefreshVehicleTitles()
    {
        for (var i = 0; i < Vehicles.Count; i++)
        {
            Vehicles[i].UpdateTitle(i);
        }
    }

    private void ApplyDetail(MembershipApplicationDetail detail)
    {
        var app = detail.Application;
        _applicationId = app.Id;
        _status = app.Status;
        PageTitle = app.Id <= 0 ? "New paper application" : $"Paper application {app.ApplicationNumber ?? $"#{app.Id}"}";
        ApplicationNumber = string.IsNullOrWhiteSpace(app.ApplicationNumber) ? "Assigned on save" : app.ApplicationNumber;

        Surname = app.Surname ?? string.Empty;
        GivenNames = app.GivenNames ?? string.Empty;
        ChildrenUnder18 = app.ChildrenUnder18 ?? string.Empty;
        Address = app.Address ?? string.Empty;
        PostCode = app.PostCode ?? string.Empty;
        _dateOfBirth = app.DateOfBirth;
        OnPropertyChanged(nameof(DateOfBirthSummary));
        Email = app.Email ?? string.Empty;
        Phone = app.Phone ?? string.Empty;
        Mobile = app.Mobile ?? string.Empty;
        AdditionalComments = app.AdditionalComments ?? string.Empty;
        PaperDeclarationSigned = app.PaperDeclarationSigned;
        _signedAt = app.SignedAt;
        SignatureDateText = app.SignedAt?.ToLocalTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

        _selectedFeeType = app.FeeType ?? _selectedFeeType;
        _selectedFeeAmount = app.SelectedFee ?? _selectedFeeAmount;
        UpdateSelectedFeeText();

        _hasNoVehicle = app.HasNoVehicle;
        OnPropertyChanged(nameof(HasNoVehicle));
        OnPropertyChanged(nameof(VehicleSectionVisible));

        Vehicles.Clear();
        if (!HasNoVehicle)
        {
            var vehicleModels = detail.Vehicles.Count > 0 ? detail.Vehicles : new[] { new MembershipApplicationVehicle { SortOrder = 0 } };
            for (var i = 0; i < vehicleModels.Count; i++)
            {
                var vm = CreateVehicleViewModel(i);
                vm.Apply(vehicleModels[i]);
                Vehicles.Add(vm);
            }
        }
    }

    private MembershipApplicationDetail BuildDetail()
    {
        var vehicles = HasNoVehicle
            ? new List<MembershipApplicationVehicle>()
            : Vehicles
                .Select((vehicle, index) => vehicle.ToModel(_applicationId, index))
                .ToList();

        return new MembershipApplicationDetail
        {
            Application = new MembershipApplication
            {
                Id = _applicationId,
                ApplicationNumber = string.IsNullOrWhiteSpace(ApplicationNumber) || ApplicationNumber == "Assigned on save"
                    ? null
                    : ApplicationNumber,
                Source = ApplicationSource.Paper,
                Status = _status,
                Surname = Surname,
                GivenNames = GivenNames,
                ChildrenUnder18 = ChildrenUnder18,
                Address = Address,
                PostCode = PostCode,
                DateOfBirth = _dateOfBirth,
                Email = Email,
                Phone = Phone,
                Mobile = Mobile,
                AdditionalComments = AdditionalComments,
                PaperDeclarationSigned = PaperDeclarationSigned,
                SelectedFee = _selectedFeeAmount,
                FeeType = _selectedFeeType,
                HasNoVehicle = HasNoVehicle,
                SignedAt = _signedAt,
            },
            Vehicles = vehicles,
        };
    }

    private void OnHasNoVehicleChanged()
    {
        if (HasNoVehicle)
        {
            Vehicles.Clear();
            return;
        }

        if (Vehicles.Count == 0)
        {
            Vehicles.Add(CreateVehicleViewModel(0));
            RefreshVehicleTitles();
        }
    }

    private async Task LoadFormContentAsync()
    {
        DeclarationText = await _formContent.GetBodyAsync(MembershipFormContentKeys.Declaration).ConfigureAwait(true) ?? string.Empty;
        FeeStructureIntro = await _formContent.GetBodyAsync(MembershipFormContentKeys.FeeStructureIntro).ConfigureAwait(true) ?? string.Empty;
        SectionApplicantDetails = await _formContent.GetBodyAsync(MembershipFormContentKeys.SectionApplicantDetails).ConfigureAwait(true) ?? SectionApplicantDetails;
        SectionVehicleDetails = await _formContent.GetBodyAsync(MembershipFormContentKeys.SectionVehicleDetails).ConfigureAwait(true) ?? SectionVehicleDetails;
        FieldAdditionalCommentsLabel = await _formContent.GetBodyAsync(MembershipFormContentKeys.FieldAdditionalComments).ConfigureAwait(true) ?? FieldAdditionalCommentsLabel;
    }

    private async Task LoadFeeDisplayAsync()
    {
        var fees = await _applications.GetFeeDisplayAsync().ConfigureAwait(true);
        FullYearFeeLine = fees.FullYearLine;
        HalfYearFeeLine = fees.HalfYearLine;
        ApplicableFeeLine = fees.ApplicableFeeLine;
        _fullYearAmount = fees.FullYearAmount;
        _halfYearAmount = fees.HalfYearAmount;
        if (_applicationId <= 0)
        {
            _selectedFeeType = fees.ApplicableFeeType;
            _selectedFeeAmount = fees.ApplicableAmount;
            UpdateSelectedFeeText();
        }
    }

    private void SelectFullYearFee()
    {
        _selectedFeeType = MembershipFeeType.FullYear;
        _selectedFeeAmount = _fullYearAmount;
        UpdateSelectedFeeText();
    }

    private void SelectHalfYearFee()
    {
        _selectedFeeType = MembershipFeeType.HalfYear;
        _selectedFeeAmount = _halfYearAmount;
        UpdateSelectedFeeText();
    }

    private async Task OverrideFeeAsync()
    {
        var result = await _inputOverlay.ShowNumpadAsync(_selectedFeeAmount, "Committee fee override", false).ConfigureAwait(true);
        if (!result.HasValue)
        {
            return;
        }

        _selectedFeeType = MembershipFeeType.Override;
        _selectedFeeAmount = decimal.Round(Math.Max(0m, result.Value), 2, MidpointRounding.AwayFromZero);
        UpdateSelectedFeeText();
    }

    private void UpdateSelectedFeeText()
    {
        var typeLabel = _selectedFeeType switch
        {
            MembershipFeeType.FullYear => "Full year",
            MembershipFeeType.HalfYear => "Half year",
            MembershipFeeType.Override => "Committee override",
            _ => "Selected",
        };
        SelectedFeeText = $"{typeLabel}: {_selectedFeeAmount.ToString("C2", CultureInfo.GetCultureInfo("en-AU"))}";
    }

    private async Task EditDigitStringAsync(string current, string title, int maxLength, Action<string> apply)
    {
        var result = await _inputOverlay.ShowDigitStringNumpadAsync(current, title, maxLength).ConfigureAwait(true);
        if (result is not null)
        {
            apply(result);
        }
    }

    private async Task EditDateOfBirthAsync() => await EditDateAsync(
        _dateOfBirth,
        "Date of birth",
        d =>
        {
            _dateOfBirth = d;
            OnPropertyChanged(nameof(DateOfBirthSummary));
        },
        maxYear: DateTime.Today.Year).ConfigureAwait(true);

    private async Task EditSignatureDateAsync()
    {
        DateOnly? current = _signedAt is { } signedAt
            ? DateOnly.FromDateTime(signedAt.LocalDateTime)
            : null;
        var result = await _inputOverlay.ShowDatePickerAsync(current, "Signature date").ConfigureAwait(true);
        if (result.Cancelled)
        {
            return;
        }

        if (result.IsCleared)
        {
            _signedAt = null;
            SignatureDateText = string.Empty;
            OnPropertyChanged(nameof(SignatureDateSummary));
            return;
        }

        if (result.HasSelection)
        {
            var d = result.Value!.Value;
            _signedAt = new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local.GetUtcOffset(d.ToDateTime(TimeOnly.MinValue)));
            SignatureDateText = MembershipDateFormats.FormatAustralianDate(d);
            OnPropertyChanged(nameof(SignatureDateSummary));
        }
    }

    private async Task EditTextAsync(string current, string title, Action<string> apply)
    {
        var result = await _inputOverlay.ShowKeyboardAsync(current, title).ConfigureAwait(true);
        if (result is not null)
        {
            apply(result.Trim());
        }
    }

    private async Task EditDateAsync(DateOnly? current, string title, Action<DateOnly> apply, int? maxYear = null)
    {
        var result = await _inputOverlay.ShowDatePickerAsync(current, title, maxYear: maxYear).ConfigureAwait(true);
        if (result.HasSelection)
        {
            apply(result.Value!.Value);
        }
    }

    private void NotifyBusyChanged()
    {
        SaveDraftCommand.NotifyCanExecuteChanged();
        SubmitCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        AddVehicleCommand.NotifyCanExecuteChanged();
        SelectFullYearFeeCommand.NotifyCanExecuteChanged();
        SelectHalfYearFeeCommand.NotifyCanExecuteChanged();
        OverrideFeeCommand.NotifyCanExecuteChanged();
    }

    private static string DisplayOrPlaceholder(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Tap to enter" : value;
}
