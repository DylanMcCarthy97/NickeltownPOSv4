using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.ViewModels.Membership;

internal static class MembershipStatusDisplay
{
    public static string Format(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Draft => "Draft",
        ApplicationStatus.Submitted => "Submitted",
        ApplicationStatus.PendingReview => "Pending review",
        ApplicationStatus.CommitteeReview => "Committee review",
        ApplicationStatus.Approved => "Approved",
        ApplicationStatus.Rejected => "Rejected",
        ApplicationStatus.AwaitingPayment => "Awaiting payment",
        ApplicationStatus.Paid => "Paid",
        ApplicationStatus.MembershipActive => "Active",
        _ => status.ToString(),
    };

    public static string FormatSource(ApplicationSource source) => source switch
    {
        ApplicationSource.Paper => "Paper",
        ApplicationSource.Online => "Online",
        ApplicationSource.Committee => "Committee",
        _ => source.ToString(),
    };
}
