using System;
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

public sealed class MembershipApplicationReviewViewModel : MembershipSubViewModelBase
{
    private readonly IMembershipApplicationReviewService _review;
    private readonly IInputOverlayService _inputOverlay;
    private readonly IUserSessionService _session;

    private long _applicationId;
    private string _pageTitle = "Application review";
    private string _applicationNumber = string.Empty;
    private string _applicantSummary = string.Empty;
    private string _contactSummary = string.Empty;
    private string _vehicleSummary = string.Empty;
    private string _feeSummary = string.Empty;

    private MembershipPaymentStatus _paymentStatus = MembershipPaymentStatus.AwaitingPayment;
    private MembershipPaymentMethod? _paymentMethod;
    private string _receiptNumber = string.Empty;
    private DateOnly? _receiptDate;
    private string _paymentEnteredBy = string.Empty;
    private string _paymentNotes = string.Empty;

    private bool _addedToMemberRegister;
    private bool _addedToEmailDistributionList;
    private bool _addedToSmsDistributionList;
    private bool _membershipCardIssued;
    private bool _welcomeBagIssued;

    private bool _approved;
    private string _approvedBy = string.Empty;
    private DateOnly? _approvalDate;
    private DateOnly? _membershipStart;
    private DateOnly? _membershipExpiry;

    private string _displayReceiptIssued = "—";
    private string _displayReceiptDate = "—";
    private string _displayMembershipAcceptedDate = "—";
    private string _displayMembershipNumber = "—";
    private string _displayMembershipStatus = "—";
    private string _displayPaymentStatus = "—";

    private string _newNoteText = string.Empty;

    public MembershipApplicationReviewViewModel(
        INavigationService navigation,
        IMembershipApplicationReviewService review,
        IInputOverlayService inputOverlay,
        IUserSessionService session)
        : base(navigation)
    {
        _review = review;
        _inputOverlay = inputOverlay;
        _session = session;

        Notes = new ObservableCollection<MembershipApplicationNoteRow>();
        Timeline = new ObservableCollection<MembershipApplicationTimelineRow>();

        SaveProcessingCommand = new AsyncRelayCommand(SaveProcessingAsync, () => !IsBusy);
        AddNoteCommand = new AsyncRelayCommand(AddNoteAsync, () => !IsBusy);
        BackCommand = new RelayCommand(Back, () => !IsBusy);

        EditPaymentEnteredByCommand = new AsyncRelayCommand(EditPaymentEnteredByAsync);
        EditPaymentNotesCommand = new AsyncRelayCommand(() => EditTextAsync(_paymentNotes, "Payment notes", v => PaymentNotes = v));
        EditApprovedByCommand = new AsyncRelayCommand(EditApprovedByAsync);
        EditReceiptDateCommand = new AsyncRelayCommand(EditReceiptDateAsync);
        EditApprovalDateCommand = new AsyncRelayCommand(EditApprovalDateAsync);
        EditMembershipStartCommand = new AsyncRelayCommand(EditMembershipStartAsync);
        EditMembershipExpiryCommand = new AsyncRelayCommand(EditMembershipExpiryAsync);
        EditNewNoteCommand = new AsyncRelayCommand(() => EditTextAsync(_newNoteText, "Committee note", v => NewNoteText = v));

        CyclePaymentMethodCommand = new RelayCommand(CyclePaymentMethod, () => !IsBusy);
    }

    public ObservableCollection<MembershipApplicationNoteRow> Notes { get; }

    public ObservableCollection<MembershipApplicationTimelineRow> Timeline { get; }

    public IAsyncRelayCommand SaveProcessingCommand { get; }

    public IAsyncRelayCommand AddNoteCommand { get; }

    public IRelayCommand BackCommand { get; }

    public IAsyncRelayCommand EditPaymentEnteredByCommand { get; }

    public IAsyncRelayCommand EditPaymentNotesCommand { get; }

    public IAsyncRelayCommand EditApprovedByCommand { get; }

    public IAsyncRelayCommand EditReceiptDateCommand { get; }

    public IAsyncRelayCommand EditApprovalDateCommand { get; }

    public IAsyncRelayCommand EditMembershipStartCommand { get; }

    public IAsyncRelayCommand EditMembershipExpiryCommand { get; }

    public IAsyncRelayCommand EditNewNoteCommand { get; }

    public IRelayCommand CyclePaymentMethodCommand { get; }

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

    public string ApplicantSummary
    {
        get => _applicantSummary;
        private set => SetProperty(ref _applicantSummary, value);
    }

    public string ContactSummary
    {
        get => _contactSummary;
        private set => SetProperty(ref _contactSummary, value);
    }

    public string VehicleSummary
    {
        get => _vehicleSummary;
        private set => SetProperty(ref _vehicleSummary, value);
    }

    public string FeeSummary
    {
        get => _feeSummary;
        private set => SetProperty(ref _feeSummary, value);
    }

    public MembershipPaymentStatus PaymentStatus
    {
        get => _paymentStatus;
        set
        {
            if (SetProperty(ref _paymentStatus, value))
            {
                OnPropertyChanged(nameof(PaymentStatusAwaiting));
                OnPropertyChanged(nameof(PaymentStatusPaid));
                OnPropertyChanged(nameof(PaymentStatusComplimentary));
                OnPropertyChanged(nameof(ReceiptNumberSummary));
                _ = EnsurePaymentReceiptFieldsAsync();
            }
        }
    }

    public bool PaymentStatusAwaiting
    {
        get => PaymentStatus == MembershipPaymentStatus.AwaitingPayment;
        set
        {
            if (value)
            {
                PaymentStatus = MembershipPaymentStatus.AwaitingPayment;
            }
        }
    }

    public bool PaymentStatusPaid
    {
        get => PaymentStatus == MembershipPaymentStatus.Paid;
        set
        {
            if (value)
            {
                PaymentStatus = MembershipPaymentStatus.Paid;
            }
        }
    }

    public bool PaymentStatusComplimentary
    {
        get => PaymentStatus == MembershipPaymentStatus.Complimentary;
        set
        {
            if (value)
            {
                PaymentStatus = MembershipPaymentStatus.Complimentary;
            }
        }
    }

    public string PaymentMethodText => MembershipDisplayFormatters.FormatPaymentMethod(_paymentMethod);

    public string ReceiptNumber
    {
        get => _receiptNumber;
        set
        {
            if (SetProperty(ref _receiptNumber, value))
            {
                OnPropertyChanged(nameof(ReceiptNumberSummary));
            }
        }
    }

    public string ReceiptDateText
    {
        get => _receiptDateText;
        private set => SetProperty(ref _receiptDateText, value);
    }

    public string PaymentEnteredBy
    {
        get => _paymentEnteredBy;
        set
        {
            if (SetProperty(ref _paymentEnteredBy, value))
            {
                OnPropertyChanged(nameof(PaymentEnteredBySummary));
            }
        }
    }

    public string PaymentNotes
    {
        get => _paymentNotes;
        set
        {
            if (SetProperty(ref _paymentNotes, value))
            {
                OnPropertyChanged(nameof(PaymentNotesSummary));
            }
        }
    }

    public bool AddedToMemberRegister
    {
        get => _addedToMemberRegister;
        set => SetProperty(ref _addedToMemberRegister, value);
    }

    public bool AddedToEmailDistributionList
    {
        get => _addedToEmailDistributionList;
        set => SetProperty(ref _addedToEmailDistributionList, value);
    }

    public bool AddedToSmsDistributionList
    {
        get => _addedToSmsDistributionList;
        set => SetProperty(ref _addedToSmsDistributionList, value);
    }

    public bool MembershipCardIssued
    {
        get => _membershipCardIssued;
        set => SetProperty(ref _membershipCardIssued, value);
    }

    public bool WelcomeBagIssued
    {
        get => _welcomeBagIssued;
        set => SetProperty(ref _welcomeBagIssued, value);
    }

    public bool Approved
    {
        get => _approved;
        set => SetProperty(ref _approved, value);
    }

    public string ApprovedBy
    {
        get => _approvedBy;
        set
        {
            if (SetProperty(ref _approvedBy, value))
            {
                OnPropertyChanged(nameof(ApprovedBySummary));
            }
        }
    }

    public string ApprovalDateText
    {
        get => _approvalDateText;
        private set => SetProperty(ref _approvalDateText, value);
    }

    public string MembershipStartText
    {
        get => _membershipStartText;
        private set => SetProperty(ref _membershipStartText, value);
    }

    public string MembershipExpiryText
    {
        get => _membershipExpiryText;
        private set => SetProperty(ref _membershipExpiryText, value);
    }

    public string DisplayReceiptIssued
    {
        get => _displayReceiptIssued;
        private set => SetProperty(ref _displayReceiptIssued, value);
    }

    public string DisplayReceiptDate
    {
        get => _displayReceiptDate;
        private set => SetProperty(ref _displayReceiptDate, value);
    }

    public string DisplayMembershipAcceptedDate
    {
        get => _displayMembershipAcceptedDate;
        private set => SetProperty(ref _displayMembershipAcceptedDate, value);
    }

    public string DisplayMembershipNumber
    {
        get => _displayMembershipNumber;
        private set => SetProperty(ref _displayMembershipNumber, value);
    }

    public string DisplayMembershipStatus
    {
        get => _displayMembershipStatus;
        private set => SetProperty(ref _displayMembershipStatus, value);
    }

    public string DisplayPaymentStatus
    {
        get => _displayPaymentStatus;
        private set => SetProperty(ref _displayPaymentStatus, value);
    }

    public string NewNoteText
    {
        get => _newNoteText;
        set
        {
            if (SetProperty(ref _newNoteText, value))
            {
                OnPropertyChanged(nameof(NewNoteSummary));
            }
        }
    }

    public string ReceiptNumberSummary => string.IsNullOrWhiteSpace(ReceiptNumber)
        ? PaymentStatus == MembershipPaymentStatus.AwaitingPayment
            ? "Auto-assigned when payment recorded"
            : "Generating…"
        : ReceiptNumber;
    public string PaymentEnteredBySummary => DisplayOrPlaceholder(PaymentEnteredBy, "Tap to fill");
    public string PaymentNotesSummary => DisplayOrPlaceholder(PaymentNotes);
    public string ApprovedBySummary => DisplayOrPlaceholder(ApprovedBy, "Tap to fill");
    public string ReceiptDateSummary => string.IsNullOrWhiteSpace(ReceiptDateText) ? "Tap to enter (dd/MM/yyyy)" : ReceiptDateText;
    public string ApprovalDateSummary => string.IsNullOrWhiteSpace(ApprovalDateText) ? "Tap to enter (dd/MM/yyyy)" : ApprovalDateText;
    public string MembershipStartSummary => string.IsNullOrWhiteSpace(MembershipStartText) ? "Tap to enter (dd/MM/yyyy)" : MembershipStartText;
    public string MembershipExpirySummary => string.IsNullOrWhiteSpace(MembershipExpiryText) ? "Tap to enter (dd/MM/yyyy)" : MembershipExpiryText;
    public string NewNoteSummary => DisplayOrPlaceholder(NewNoteText);

    private string _receiptDateText = string.Empty;
    private string _approvalDateText = string.Empty;
    private string _membershipStartText = string.Empty;
    private string _membershipExpiryText = string.Empty;

    public async Task LoadAsync(long applicationId)
    {
        try
        {
            IsBusy = true;
            _applicationId = applicationId;
            var detail = await _review.GetReviewDetailAsync(applicationId).ConfigureAwait(true)
                ?? throw new InvalidOperationException("Application not found.");

            ApplyDetail(detail);
            SetStatus("Review loaded.");
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

    private void ApplyDetail(MembershipApplicationReviewDetail detail)
    {
        var app = detail.Application;
        PageTitle = $"Review {app.ApplicationNumber ?? $"#{app.Id}"}";
        ApplicationNumber = app.ApplicationNumber ?? $"#{app.Id}";
        ApplicantSummary = $"{app.GivenNames} {app.Surname}".Trim();
        ContactSummary = BuildContactSummary(app);
        VehicleSummary = BuildVehicleSummary(detail);
        FeeSummary = app.SelectedFee.HasValue
            ? $"{app.FeeType}: {app.SelectedFee.Value.ToString("C2", CultureInfo.GetCultureInfo("en-AU"))}"
            : "—";

        PaymentStatus = app.PaymentStatus;
        _paymentMethod = app.PaymentMethod;
        OnPropertyChanged(nameof(PaymentMethodText));
        ReceiptNumber = app.ReceiptNumber ?? string.Empty;
        _receiptDate = app.ReceiptDate;
        ReceiptDateText = MembershipDateFormats.FormatAustralianDate(app.ReceiptDate);
        PaymentEnteredBy = app.PaymentEnteredBy ?? string.Empty;
        PaymentNotes = app.PaymentNotes ?? string.Empty;

        AddedToMemberRegister = app.AddedToMemberRegister;
        AddedToEmailDistributionList = app.AddedToEmailDistributionList;
        AddedToSmsDistributionList = app.AddedToSmsDistributionList;
        MembershipCardIssued = app.MembershipCardIssued;
        WelcomeBagIssued = app.WelcomeBagIssued;

        Approved = app.Status is ApplicationStatus.Approved or ApplicationStatus.Paid or ApplicationStatus.MembershipActive
            || app.ApprovedAt.HasValue;
        ApprovedBy = app.ApprovedBy ?? string.Empty;
        _approvalDate = app.ApprovalDate ?? app.MembershipAcceptedDate;
        ApprovalDateText = MembershipDateFormats.FormatAustralianDate(_approvalDate);
        _membershipStart = app.MembershipStart;
        MembershipStartText = MembershipDateFormats.FormatAustralianDate(app.MembershipStart);
        _membershipExpiry = app.MembershipExpiry;
        MembershipExpiryText = MembershipDateFormats.FormatAustralianDate(app.MembershipExpiry);

        UpdateDisplayFields(app);

        Notes.Clear();
        foreach (var note in detail.Notes)
        {
            Notes.Add(MembershipApplicationNoteRow.From(note));
        }

        Timeline.Clear();
        foreach (var entry in detail.TimelineEvents)
        {
            Timeline.Add(MembershipApplicationTimelineRow.From(entry));
        }
    }

    private void UpdateDisplayFields(MembershipApplication app)
    {
        DisplayReceiptIssued = app.ReceiptIssued ? "Yes" : "No";
        DisplayReceiptDate = MembershipDateFormats.FormatAustralianDate(app.ReceiptDate) is { Length: > 0 } rd ? rd : "—";
        DisplayMembershipAcceptedDate = MembershipDateFormats.FormatAustralianDate(app.MembershipAcceptedDate) is { Length: > 0 } mad ? mad : "—";
        DisplayMembershipNumber = string.IsNullOrWhiteSpace(app.MembershipNumber) ? "—" : app.MembershipNumber;
        DisplayMembershipStatus = MembershipStatusDisplay.Format(app.Status);
        DisplayPaymentStatus = MembershipDisplayFormatters.FormatPaymentStatus(app.PaymentStatus);
    }

    private async Task SaveProcessingAsync()
    {
        try
        {
            IsBusy = true;
            NotifyBusyChanged();

            var request = new MembershipApplicationReviewSaveRequest
            {
                ApplicationId = _applicationId,
                PaymentStatus = PaymentStatus,
                PaymentMethod = _paymentMethod,
                ReceiptNumber = ReceiptNumber,
                ReceiptDate = _receiptDate,
                PaymentEnteredBy = PaymentEnteredBy,
                PaymentNotes = PaymentNotes,
                AddedToMemberRegister = AddedToMemberRegister,
                AddedToEmailDistributionList = AddedToEmailDistributionList,
                AddedToSmsDistributionList = AddedToSmsDistributionList,
                MembershipCardIssued = MembershipCardIssued,
                WelcomeBagIssued = WelcomeBagIssued,
                Approved = Approved,
                ApprovedBy = ApprovedBy,
                ApprovalDate = _approvalDate,
                MembershipStart = _membershipStart,
                MembershipExpiry = _membershipExpiry,
            };

            var detail = await _review.SaveProcessingAsync(request).ConfigureAwait(true);
            ApplyDetail(detail);
            SetStatus("Processing saved.");
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

    private async Task AddNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteText))
        {
            SetStatus("Enter a note before adding.");
            return;
        }

        try
        {
            IsBusy = true;
            NotifyBusyChanged();
            var note = await _review.AddNoteAsync(_applicationId, NewNoteText).ConfigureAwait(true);
            Notes.Insert(0, MembershipApplicationNoteRow.From(note));
            NewNoteText = string.Empty;
            SetStatus("Note added.");
        }
        catch (Exception ex)
        {
            SetStatus($"Add note failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            NotifyBusyChanged();
        }
    }

    private void Back() => Navigate(typeof(MembershipApplicationsPage));

    private void CyclePaymentMethod()
    {
        var values = Enum.GetValues<MembershipPaymentMethod>();
        if (_paymentMethod is null)
        {
            _paymentMethod = values[0];
        }
        else
        {
            var index = Array.IndexOf(values, _paymentMethod.Value);
            _paymentMethod = values[(index + 1) % values.Length];
        }

        OnPropertyChanged(nameof(PaymentMethodText));
    }

    private static string BuildContactSummary(MembershipApplication app)
    {
        var parts = new[] { app.Phone, app.Mobile, app.Email }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        return parts.Length == 0 ? "—" : string.Join(" · ", parts);
    }

    private static string BuildVehicleSummary(MembershipApplicationReviewDetail detail)
    {
        if (detail.Application.HasNoVehicle)
        {
            return "No vehicle";
        }

        var registrations = detail.Vehicles
            .Select(v => v.RegistrationNumber)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToArray();
        return registrations.Length == 0 ? "—" : string.Join(", ", registrations);
    }

    private async Task EditPaymentEnteredByAsync() =>
        await EditStaffNameAsync(_paymentEnteredBy, "Entered by", v => PaymentEnteredBy = v).ConfigureAwait(true);

    private async Task EditApprovedByAsync() =>
        await EditStaffNameAsync(_approvedBy, "Approved by", v => ApprovedBy = v).ConfigureAwait(true);

    private async Task EditStaffNameAsync(string current, string title, Action<string> apply)
    {
        var value = current;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = ResolveCurrentUserDisplayName() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
            {
                apply(value);
            }
        }

        var result = await _inputOverlay.ShowKeyboardAsync(value, title).ConfigureAwait(true);
        if (result is not null)
        {
            apply(result.Trim());
        }
    }

    private string? ResolveCurrentUserDisplayName()
    {
        if (_session.IsSignedIn && !string.IsNullOrWhiteSpace(_session.DisplayName))
        {
            return _session.DisplayName.Trim();
        }

        return null;
    }

    private async Task EnsurePaymentReceiptFieldsAsync()
    {
        if (PaymentStatus != MembershipPaymentStatus.Paid
            && PaymentStatus != MembershipPaymentStatus.Complimentary)
        {
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(ReceiptNumber))
            {
                ReceiptNumber = await _review.GenerateNextReceiptNumberAsync().ConfigureAwait(true);
            }

            if (!_receiptDate.HasValue)
            {
                _receiptDate = DateOnly.FromDateTime(DateTime.Now);
                ReceiptDateText = MembershipDateFormats.FormatAustralianDate(_receiptDate);
                OnPropertyChanged(nameof(ReceiptDateSummary));
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Receipt setup failed: {ex.Message}");
        }
    }

    private async Task EditReceiptDateAsync() => await EditOptionalDateAsync(_receiptDate, "Receipt date", d =>
    {
        _receiptDate = d;
        ReceiptDateText = MembershipDateFormats.FormatAustralianDate(d);
        OnPropertyChanged(nameof(ReceiptDateSummary));
    }).ConfigureAwait(true);

    private async Task EditApprovalDateAsync() => await EditOptionalDateAsync(_approvalDate, "Approval date", d =>
    {
        _approvalDate = d;
        ApprovalDateText = MembershipDateFormats.FormatAustralianDate(d);
        OnPropertyChanged(nameof(ApprovalDateSummary));
    }).ConfigureAwait(true);

    private async Task EditMembershipStartAsync() => await EditOptionalDateAsync(_membershipStart, "Membership start", d =>
    {
        _membershipStart = d;
        MembershipStartText = MembershipDateFormats.FormatAustralianDate(d);
        OnPropertyChanged(nameof(MembershipStartSummary));
    }).ConfigureAwait(true);

    private async Task EditMembershipExpiryAsync() => await EditOptionalDateAsync(_membershipExpiry, "Membership expiry", d =>
    {
        _membershipExpiry = d;
        MembershipExpiryText = MembershipDateFormats.FormatAustralianDate(d);
        OnPropertyChanged(nameof(MembershipExpirySummary));
    }).ConfigureAwait(true);

    private async Task EditTextAsync(string current, string title, Action<string> apply)
    {
        var result = await _inputOverlay.ShowKeyboardAsync(current, title).ConfigureAwait(true);
        if (result is not null)
        {
            apply(result.Trim());
        }
    }

    private async Task EditOptionalDateAsync(DateOnly? current, string title, Action<DateOnly?> apply)
    {
        var result = await _inputOverlay.ShowDatePickerAsync(current, title).ConfigureAwait(true);
        if (result.Cancelled)
        {
            return;
        }

        apply(result.IsCleared ? null : result.Value);
    }

    private void NotifyBusyChanged()
    {
        SaveProcessingCommand.NotifyCanExecuteChanged();
        AddNoteCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
        CyclePaymentMethodCommand.NotifyCanExecuteChanged();
    }

    private static string DisplayOrPlaceholder(string value, string placeholder = "Tap to enter") =>
        string.IsNullOrWhiteSpace(value) ? placeholder : value;
}

public sealed class MembershipApplicationNoteRow
{
    public string Author { get; init; } = string.Empty;

    public string DateTimeText { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public static MembershipApplicationNoteRow From(MembershipApplicationNote note)
    {
        var local = note.CreatedAt.ToLocalTime();
        return new MembershipApplicationNoteRow
        {
            Author = note.Author,
            DateTimeText = local.ToString("d MMM yyyy h:mm tt", CultureInfo.CurrentCulture),
            Text = note.Text,
        };
    }
}

public sealed class MembershipApplicationTimelineRow
{
    public string EventTitle { get; init; } = string.Empty;

    public string User { get; init; } = string.Empty;

    public string DateTimeText { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public static MembershipApplicationTimelineRow From(MembershipApplicationTimelineEvent entry)
    {
        var local = entry.OccurredAt.ToLocalTime();
        return new MembershipApplicationTimelineRow
        {
            EventTitle = MembershipDisplayFormatters.FormatTimelineEventType(entry.EventType),
            User = entry.User,
            DateTimeText = local.ToString("d MMM yyyy h:mm tt", CultureInfo.CurrentCulture),
            Description = entry.Description,
        };
    }
}
