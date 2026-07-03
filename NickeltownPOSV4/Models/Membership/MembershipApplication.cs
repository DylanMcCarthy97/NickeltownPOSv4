using System;

namespace NickeltownPOSV4.Models.Membership;

public sealed class MembershipApplication
{
    public long Id { get; init; }

    public string? ApplicationNumber { get; init; }

    public ApplicationSource Source { get; init; }

    public ApplicationStatus Status { get; init; }

    public string? Surname { get; init; }

    public string? GivenNames { get; init; }

    public string? ChildrenUnder18 { get; init; }

    public string? Address { get; init; }

    public string? PostCode { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? Mobile { get; init; }

    public string? AdditionalComments { get; init; }

    public bool PaperDeclarationSigned { get; init; }

    public decimal? SelectedFee { get; init; }

    public MembershipFeeType? FeeType { get; init; }

    public bool ReceiptIssued { get; init; }

    public DateOnly? ReceiptDate { get; init; }

    public DateOnly? MembershipAcceptedDate { get; init; }

    public bool AddedToDistributionList { get; init; }

    public bool AddedToMemberRegister { get; init; }

    public bool AddedToEmailDistributionList { get; init; }

    public bool AddedToSmsDistributionList { get; init; }

    public bool MembershipCardIssued { get; init; }

    public bool WelcomeBagIssued { get; init; }

    public bool HasNoVehicle { get; init; }

    public MembershipPaymentStatus PaymentStatus { get; init; } = MembershipPaymentStatus.AwaitingPayment;

    public MembershipPaymentMethod? PaymentMethod { get; init; }

    public string? ReceiptNumber { get; init; }

    public string? PaymentEnteredBy { get; init; }

    public string? PaymentNotes { get; init; }

    public string? ApprovedBy { get; init; }

    public DateOnly? ApprovalDate { get; init; }

    public DateOnly? MembershipStart { get; init; }

    public DateOnly? MembershipExpiry { get; init; }

    public string? MembershipNumber { get; init; }

    public string? CreatedBy { get; init; }

    public string? SignatureData { get; init; }

    public DateTimeOffset? SignedAt { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset? ReviewedAt { get; init; }

    public DateTimeOffset? ApprovedAt { get; init; }

    public DateTimeOffset? RejectedAt { get; init; }

    public string? RejectionReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
