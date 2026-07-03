namespace NickeltownPOSV4.Models.Membership;

public enum ApplicationStatus
{
    Draft,
    Submitted,
    PendingReview,
    CommitteeReview,
    Approved,
    Rejected,
    AwaitingPayment,
    Paid,
    MembershipActive,
}
