using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public interface IMembershipApplicationReviewService
{
    Task<MembershipApplicationReviewDetail?> GetReviewDetailAsync(long applicationId, CancellationToken cancellationToken = default);

    Task<MembershipApplicationReviewDetail> SaveProcessingAsync(MembershipApplicationReviewSaveRequest request, CancellationToken cancellationToken = default);

    Task<MembershipApplicationNote> AddNoteAsync(long applicationId, string text, CancellationToken cancellationToken = default);

    Task<string> GenerateNextReceiptNumberAsync(CancellationToken cancellationToken = default);
}

public sealed class MembershipApplicationReviewSaveRequest
{
    public long ApplicationId { get; init; }

    public MembershipPaymentStatus PaymentStatus { get; init; }

    public MembershipPaymentMethod? PaymentMethod { get; init; }

    public string? ReceiptNumber { get; init; }

    public DateOnly? ReceiptDate { get; init; }

    public string? PaymentEnteredBy { get; init; }

    public string? PaymentNotes { get; init; }

    public bool AddedToMemberRegister { get; init; }

    public bool AddedToEmailDistributionList { get; init; }

    public bool AddedToSmsDistributionList { get; init; }

    public bool MembershipCardIssued { get; init; }

    public bool WelcomeBagIssued { get; init; }

    public bool Approved { get; init; }

    public string? ApprovedBy { get; init; }

    public DateOnly? ApprovalDate { get; init; }

    public DateOnly? MembershipStart { get; init; }

    public DateOnly? MembershipExpiry { get; init; }
}
