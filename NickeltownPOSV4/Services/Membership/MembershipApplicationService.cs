using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Membership;
using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.Services.Membership;

public sealed class MembershipApplicationService : IMembershipApplicationService
{
    private readonly IMembershipApplicationRepository _applications;
    private readonly IMembershipSettingsRepository _settings;
    private readonly IMembershipFormContentRepository _formContent;
    private readonly IUserSessionService _session;

    public MembershipApplicationService(
        IMembershipApplicationRepository applications,
        IMembershipSettingsRepository settings,
        IMembershipFormContentRepository formContent,
        IUserSessionService session)
    {
        _applications = applications;
        _settings = settings;
        _formContent = formContent;
        _session = session;
    }

    public Task<IReadOnlyList<MembershipApplicationListItem>> ListAsync(CancellationToken cancellationToken = default) =>
        _applications.ListAsync(cancellationToken);

    public async Task<MembershipApplicationDetail?> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var application = await _applications.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return null;
        }

        var vehicles = await _applications.GetVehiclesAsync(id, cancellationToken).ConfigureAwait(false);
        return new MembershipApplicationDetail
        {
            Application = application,
            Vehicles = vehicles,
        };
    }

    public async Task<MembershipApplicationDetail> CreateNewPaperApplicationAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var feeType = MembershipFeeCalculator.GetApplicableFeeType(today);
        var feeAmount = MembershipFeeCalculator.GetApplicableAmount(settings, today);
        var now = DateTimeOffset.UtcNow;

        return new MembershipApplicationDetail
        {
            Application = new MembershipApplication
            {
                Source = ApplicationSource.Paper,
                Status = ApplicationStatus.Draft,
                SubmittedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = ResolveCreatedBy(),
                FeeType = feeType,
                SelectedFee = feeAmount,
            },
            Vehicles =
            [
                new MembershipApplicationVehicle { SortOrder = 0 },
            ],
        };
    }

    public async Task<MembershipApplicationDetail> SaveDraftAsync(MembershipApplicationDetail detail, CancellationToken cancellationToken = default)
    {
        var normalized = await NormalizeAsync(detail, ApplicationStatus.Draft, setSubmittedAt: false, cancellationToken).ConfigureAwait(false);
        return await PersistAsync(normalized, submittedStatus: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MembershipApplicationDetail> SubmitForReviewAsync(MembershipApplicationDetail detail, CancellationToken cancellationToken = default)
    {
        var validation = ValidateForSubmit(detail);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(" ", validation.Errors));
        }

        var normalized = await NormalizeAsync(detail, ApplicationStatus.PendingReview, setSubmittedAt: true, cancellationToken).ConfigureAwait(false);
        return await PersistAsync(normalized, submittedStatus: ApplicationStatus.PendingReview, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteApplicationAsync(long id, CancellationToken cancellationToken = default) =>
        _applications.DeleteAsync(id, cancellationToken);

    public MembershipApplicationValidationResult ValidateForSubmit(MembershipApplicationDetail detail)
    {
        var errors = new List<string>();
        var app = detail.Application;

        if (string.IsNullOrWhiteSpace(app.Surname))
        {
            errors.Add("Surname is required.");
        }

        if (string.IsNullOrWhiteSpace(app.GivenNames))
        {
            errors.Add("Given names are required.");
        }

        if (string.IsNullOrWhiteSpace(app.Address))
        {
            errors.Add("Address is required.");
        }

        if (string.IsNullOrWhiteSpace(app.Phone) && string.IsNullOrWhiteSpace(app.Mobile))
        {
            errors.Add("Phone or mobile is required.");
        }

        if (detail.Vehicles.Count == 0 && !app.HasNoVehicle)
        {
            errors.Add("At least one vehicle is required.");
        }

        if (!app.PaperDeclarationSigned)
        {
            errors.Add("Applicant signed paper declaration must be checked.");
        }

        return new MembershipApplicationValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
        };
    }

    public async Task<MembershipFeeDisplay> GetFeeDisplayAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var fullLabel = await GetFormBodyAsync(MembershipFormContentKeys.FeeStructureJulyDecember, cancellationToken).ConfigureAwait(false);
        var halfLabel = await GetFormBodyAsync(MembershipFormContentKeys.FeeStructureJanuaryJune, cancellationToken).ConfigureAwait(false);
        var applicableType = MembershipFeeCalculator.GetApplicableFeeType(today);
        var applicableAmount = MembershipFeeCalculator.GetApplicableAmount(settings, today);

        return new MembershipFeeDisplay
        {
            FullYearLine = $"{fullLabel} - {MembershipFeeCalculator.FormatMoney(settings.JoiningFeeFull)} joining fee",
            HalfYearLine = $"{halfLabel} - {MembershipFeeCalculator.FormatMoney(settings.JoiningFeeHalf)} joining fee",
            ApplicableFeeLine = $"Current applicable fee ({today:dd MMM yyyy}): {MembershipFeeCalculator.FormatMoney(applicableAmount)}",
            FullYearAmount = settings.JoiningFeeFull,
            HalfYearAmount = settings.JoiningFeeHalf,
            ApplicableAmount = applicableAmount,
            ApplicableFeeType = applicableType,
        };
    }

    private async Task<MembershipApplicationDetail> NormalizeAsync(
        MembershipApplicationDetail detail,
        ApplicationStatus status,
        bool setSubmittedAt,
        CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var app = detail.Application;
        MembershipApplication? existing = null;
        if (app.Id > 0)
        {
            existing = await _applications.GetByIdAsync(app.Id, cancellationToken).ConfigureAwait(false);
        }

        var feeType = app.FeeType ?? MembershipFeeCalculator.GetApplicableFeeType(DateOnly.FromDateTime(DateTime.Now));
        var selectedFee = MembershipFeeCalculator.ResolveSelectedAmount(settings, feeType, app.SelectedFee);
        var now = DateTimeOffset.UtcNow;
        var applicationNumber = app.ApplicationNumber;
        if (string.IsNullOrWhiteSpace(applicationNumber))
        {
            applicationNumber = await _applications.GenerateNextApplicationNumberAsync(cancellationToken).ConfigureAwait(false);
        }

        var committee = existing ?? app;
        var normalizedApp = new MembershipApplication
        {
            Id = app.Id,
            ApplicationNumber = applicationNumber,
            Source = ApplicationSource.Paper,
            Status = status,
            Surname = TrimOrNull(app.Surname),
            GivenNames = TrimOrNull(app.GivenNames),
            ChildrenUnder18 = TrimOrNull(app.ChildrenUnder18),
            Address = TrimOrNull(app.Address),
            PostCode = TrimOrNull(app.PostCode),
            DateOfBirth = app.DateOfBirth,
            Email = TrimOrNull(app.Email),
            Phone = TrimOrNull(app.Phone),
            Mobile = TrimOrNull(app.Mobile),
            AdditionalComments = TrimOrNull(app.AdditionalComments),
            PaperDeclarationSigned = app.PaperDeclarationSigned,
            SelectedFee = selectedFee,
            FeeType = feeType,
            ReceiptIssued = committee.ReceiptIssued,
            ReceiptDate = committee.ReceiptDate,
            MembershipAcceptedDate = committee.MembershipAcceptedDate,
            AddedToDistributionList = committee.AddedToDistributionList,
            AddedToMemberRegister = committee.AddedToMemberRegister,
            AddedToEmailDistributionList = committee.AddedToEmailDistributionList,
            AddedToSmsDistributionList = committee.AddedToSmsDistributionList,
            MembershipCardIssued = committee.MembershipCardIssued,
            WelcomeBagIssued = committee.WelcomeBagIssued,
            HasNoVehicle = app.HasNoVehicle,
            PaymentStatus = committee.PaymentStatus,
            PaymentMethod = committee.PaymentMethod,
            ReceiptNumber = committee.ReceiptNumber,
            PaymentEnteredBy = committee.PaymentEnteredBy,
            PaymentNotes = committee.PaymentNotes,
            ApprovedBy = committee.ApprovedBy,
            ApprovalDate = committee.ApprovalDate,
            MembershipStart = committee.MembershipStart,
            MembershipExpiry = committee.MembershipExpiry,
            MembershipNumber = committee.MembershipNumber,
            CreatedBy = string.IsNullOrWhiteSpace(app.CreatedBy) ? ResolveCreatedBy() : app.CreatedBy,
            SignatureData = app.SignatureData,
            SignedAt = app.SignedAt,
            SubmittedAt = setSubmittedAt ? now : app.SubmittedAt == default ? now : app.SubmittedAt,
            ReviewedAt = committee.ReviewedAt,
            ApprovedAt = committee.ApprovedAt,
            RejectedAt = committee.RejectedAt,
            RejectionReason = committee.RejectionReason,
            CreatedAt = app.CreatedAt == default ? now : app.CreatedAt,
            UpdatedAt = now,
        };

        var vehicles = app.HasNoVehicle
            ? new List<MembershipApplicationVehicle>()
            : detail.Vehicles
                .Select((vehicle, index) => new MembershipApplicationVehicle
                {
                    ApplicationId = normalizedApp.Id,
                    MakeModel = TrimOrNull(vehicle.MakeModel),
                    Year = TrimOrNull(vehicle.Year),
                    BodyType = TrimOrNull(vehicle.BodyType),
                    Engine = TrimOrNull(vehicle.Engine),
                    RegistrationNumber = TrimOrNull(vehicle.RegistrationNumber),
                    ClubRego = TrimOrNull(vehicle.ClubRego),
                    Colour = TrimOrNull(vehicle.Colour),
                    Modifications = TrimOrNull(vehicle.Modifications),
                    SortOrder = index,
                })
                .ToList();

        return new MembershipApplicationDetail
        {
            Application = normalizedApp,
            Vehicles = vehicles,
        };
    }

    private async Task<MembershipApplicationDetail> PersistAsync(
        MembershipApplicationDetail detail,
        ApplicationStatus? submittedStatus,
        CancellationToken cancellationToken)
    {
        var app = detail.Application;
        var isNew = app.Id <= 0;
        long id;
        if (isNew)
        {
            id = await _applications.InsertAsync(app, cancellationToken).ConfigureAwait(false);
            await RecordTimelineEventAsync(
                id,
                MembershipTimelineEventType.ApplicationCreated,
                "Paper application created.",
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _applications.UpdateAsync(app, cancellationToken).ConfigureAwait(false);
            id = app.Id;
            if (submittedStatus == ApplicationStatus.PendingReview)
            {
                await RecordTimelineEventAsync(
                    id,
                    MembershipTimelineEventType.Submitted,
                    "Application submitted for committee review.",
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await RecordTimelineEventAsync(
                    id,
                    MembershipTimelineEventType.Edited,
                    "Application details updated.",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await _applications.ReplaceVehiclesAsync(id, detail.Vehicles, cancellationToken).ConfigureAwait(false);
        var saved = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        return saved ?? throw new InvalidOperationException("Saved application could not be loaded.");
    }

    private async Task RecordTimelineEventAsync(
        long applicationId,
        MembershipTimelineEventType eventType,
        string description,
        CancellationToken cancellationToken)
    {
        await _applications.InsertTimelineEventAsync(
            new MembershipApplicationTimelineEvent
            {
                ApplicationId = applicationId,
                EventType = eventType,
                User = ResolveCreatedBy() ?? "Staff",
                Description = description,
                OccurredAt = DateTimeOffset.UtcNow,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private string? ResolveCreatedBy()
    {
        if (_session.IsSignedIn && !string.IsNullOrWhiteSpace(_session.DisplayName))
        {
            return _session.DisplayName.Trim();
        }

        return null;
    }

    private async Task<string> GetFormBodyAsync(string key, CancellationToken cancellationToken)
    {
        var section = await _formContent.GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
        return section?.Body?.Trim() ?? key;
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
