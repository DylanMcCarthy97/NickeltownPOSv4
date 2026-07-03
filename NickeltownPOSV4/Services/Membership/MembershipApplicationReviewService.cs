using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public sealed class MembershipApplicationReviewService : IMembershipApplicationReviewService
{
    private readonly IMembershipApplicationRepository _applications;
    private readonly IMembershipSettingsRepository _settings;
    private readonly IUserSessionService _session;

    public MembershipApplicationReviewService(
        IMembershipApplicationRepository applications,
        IMembershipSettingsRepository settings,
        IUserSessionService session)
    {
        _applications = applications;
        _settings = settings;
        _session = session;
    }

    public async Task<MembershipApplicationReviewDetail?> GetReviewDetailAsync(long applicationId, CancellationToken cancellationToken = default)
    {
        var application = await _applications.GetByIdAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return null;
        }

        var vehicles = await _applications.GetVehiclesAsync(applicationId, cancellationToken).ConfigureAwait(false);
        var notes = await _applications.GetNotesAsync(applicationId, cancellationToken).ConfigureAwait(false);
        var timeline = await _applications.GetTimelineEventsAsync(applicationId, cancellationToken).ConfigureAwait(false);

        return new MembershipApplicationReviewDetail
        {
            Application = application,
            Vehicles = vehicles,
            Notes = notes,
            TimelineEvents = timeline,
        };
    }

    public async Task<MembershipApplicationReviewDetail> SaveProcessingAsync(
        MembershipApplicationReviewSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _applications.GetByIdAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Application not found.");

        var now = DateTimeOffset.UtcNow;
        var user = ResolveCurrentUser();
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);

        var wasApproved = existing.Status == ApplicationStatus.Approved
            || existing.Status == ApplicationStatus.MembershipActive
            || existing.ApprovedAt.HasValue;
        var wasPaid = existing.PaymentStatus == MembershipPaymentStatus.Paid
            || existing.PaymentStatus == MembershipPaymentStatus.Complimentary;
        var wasCardIssued = existing.MembershipCardIssued;
        var wasOnRegister = existing.AddedToMemberRegister;

        var receiptIssued = request.PaymentStatus is MembershipPaymentStatus.Paid or MembershipPaymentStatus.Complimentary;
        var receiptNumber = TrimOrNull(request.ReceiptNumber);
        if (receiptIssued && string.IsNullOrWhiteSpace(receiptNumber))
        {
            receiptNumber = await _applications.GenerateNextReceiptNumberAsync(cancellationToken).ConfigureAwait(false);
        }

        var membershipNumber = existing.MembershipNumber;
        var approvedAt = existing.ApprovedAt;
        var status = existing.Status;

        if (request.Approved)
        {
            if (string.IsNullOrWhiteSpace(membershipNumber))
            {
                membershipNumber = await _applications.GenerateNextMembershipNumberAsync(cancellationToken).ConfigureAwait(false);
            }

            approvedAt ??= now;
            status = ApplicationStatus.Approved;
        }

        if (request.PaymentStatus == MembershipPaymentStatus.Paid && !wasPaid)
        {
            status = status == ApplicationStatus.MembershipActive ? status : ApplicationStatus.Paid;
        }
        else if (request.PaymentStatus == MembershipPaymentStatus.Complimentary && !wasPaid)
        {
            status = status == ApplicationStatus.MembershipActive ? status : ApplicationStatus.Paid;
        }

        if (request.Approved
            && request.PaymentStatus is MembershipPaymentStatus.Paid or MembershipPaymentStatus.Complimentary
            && request.MembershipCardIssued
            && request.AddedToMemberRegister)
        {
            status = ApplicationStatus.MembershipActive;
        }

        var updated = new MembershipApplication
        {
            Id = existing.Id,
            ApplicationNumber = existing.ApplicationNumber,
            Source = existing.Source,
            Status = status,
            Surname = existing.Surname,
            GivenNames = existing.GivenNames,
            ChildrenUnder18 = existing.ChildrenUnder18,
            Address = existing.Address,
            PostCode = existing.PostCode,
            DateOfBirth = existing.DateOfBirth,
            Email = existing.Email,
            Phone = existing.Phone,
            Mobile = existing.Mobile,
            AdditionalComments = existing.AdditionalComments,
            PaperDeclarationSigned = existing.PaperDeclarationSigned,
            SelectedFee = existing.SelectedFee,
            FeeType = existing.FeeType,
            ReceiptIssued = receiptIssued,
            ReceiptDate = request.ReceiptDate ?? existing.ReceiptDate,
            MembershipAcceptedDate = request.Approved ? request.ApprovalDate ?? existing.MembershipAcceptedDate : existing.MembershipAcceptedDate,
            AddedToDistributionList = existing.AddedToDistributionList,
            AddedToMemberRegister = request.AddedToMemberRegister,
            AddedToEmailDistributionList = request.AddedToEmailDistributionList,
            AddedToSmsDistributionList = request.AddedToSmsDistributionList,
            MembershipCardIssued = request.MembershipCardIssued,
            WelcomeBagIssued = request.WelcomeBagIssued,
            HasNoVehicle = existing.HasNoVehicle,
            PaymentStatus = request.PaymentStatus,
            PaymentMethod = request.PaymentMethod,
            ReceiptNumber = receiptNumber,
            PaymentEnteredBy = TrimOrNull(request.PaymentEnteredBy) ?? user,
            PaymentNotes = TrimOrNull(request.PaymentNotes),
            ApprovedBy = request.Approved ? TrimOrNull(request.ApprovedBy) ?? user : existing.ApprovedBy,
            ApprovalDate = request.Approved ? request.ApprovalDate ?? DateOnly.FromDateTime(DateTime.Now) : existing.ApprovalDate,
            MembershipStart = request.Approved
                ? request.MembershipStart ?? settings.MembershipYearStart
                : existing.MembershipStart,
            MembershipExpiry = request.Approved
                ? request.MembershipExpiry ?? settings.MembershipYearEnd
                : existing.MembershipExpiry,
            MembershipNumber = membershipNumber,
            CreatedBy = existing.CreatedBy,
            SignatureData = existing.SignatureData,
            SignedAt = existing.SignedAt,
            SubmittedAt = existing.SubmittedAt,
            ReviewedAt = existing.ReviewedAt ?? now,
            ApprovedAt = request.Approved ? approvedAt : existing.ApprovedAt,
            RejectedAt = existing.RejectedAt,
            RejectionReason = existing.RejectionReason,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = now,
        };

        await _applications.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);

        if (request.Approved && !wasApproved)
        {
            await RecordTimelineEventAsync(
                request.ApplicationId,
                MembershipTimelineEventType.Approved,
                user,
                $"Membership approved. Number assigned: {membershipNumber}.",
                cancellationToken).ConfigureAwait(false);
        }

        var isPaid = request.PaymentStatus is MembershipPaymentStatus.Paid or MembershipPaymentStatus.Complimentary;
        if (isPaid && !wasPaid)
        {
            await RecordTimelineEventAsync(
                request.ApplicationId,
                MembershipTimelineEventType.MarkedPaid,
                user,
                $"Payment marked {MembershipDisplayFormatters.FormatPaymentStatus(request.PaymentStatus)}.",
                cancellationToken).ConfigureAwait(false);
        }

        if (request.MembershipCardIssued && !wasCardIssued)
        {
            await RecordTimelineEventAsync(
                request.ApplicationId,
                MembershipTimelineEventType.CardIssued,
                user,
                "Membership card issued.",
                cancellationToken).ConfigureAwait(false);
        }

        if (request.AddedToMemberRegister && !wasOnRegister)
        {
            await RecordTimelineEventAsync(
                request.ApplicationId,
                MembershipTimelineEventType.AddedToRegister,
                user,
                "Added to member register.",
                cancellationToken).ConfigureAwait(false);
        }

        if (updated.Status == ApplicationStatus.MembershipActive && existing.Status != ApplicationStatus.MembershipActive)
        {
            await RecordTimelineEventAsync(
                request.ApplicationId,
                MembershipTimelineEventType.MembershipActivated,
                user,
                "Membership activated.",
                cancellationToken).ConfigureAwait(false);
        }

        return await GetReviewDetailAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Saved review could not be loaded.");
    }

    public async Task<MembershipApplicationNote> AddNoteAsync(long applicationId, string text, CancellationToken cancellationToken = default)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidOperationException("Note text is required.");
        }

        var note = new MembershipApplicationNote
        {
            ApplicationId = applicationId,
            Author = ResolveCurrentUser() ?? "Staff",
            Text = trimmed,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var id = await _applications.InsertNoteAsync(note, cancellationToken).ConfigureAwait(false);
        return new MembershipApplicationNote
        {
            Id = id,
            ApplicationId = note.ApplicationId,
            Author = note.Author,
            Text = note.Text,
            CreatedAt = note.CreatedAt,
        };
    }

    public Task<string> GenerateNextReceiptNumberAsync(CancellationToken cancellationToken = default) =>
        _applications.GenerateNextReceiptNumberAsync(cancellationToken);

    private async Task RecordTimelineEventAsync(
        long applicationId,
        MembershipTimelineEventType eventType,
        string? user,
        string description,
        CancellationToken cancellationToken)
    {
        await _applications.InsertTimelineEventAsync(
            new MembershipApplicationTimelineEvent
            {
                ApplicationId = applicationId,
                EventType = eventType,
                User = user ?? "Staff",
                Description = description,
                OccurredAt = DateTimeOffset.UtcNow,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private string? ResolveCurrentUser()
    {
        if (_session.IsSignedIn && !string.IsNullOrWhiteSpace(_session.DisplayName))
        {
            return _session.DisplayName.Trim();
        }

        return null;
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
