using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

internal static class MembershipApplicationDeleteRules
{
    public static bool CanDelete(ApplicationStatus status, MembershipPaymentStatus paymentStatus, bool hasLinkedMember)
    {
        if (hasLinkedMember)
        {
            return false;
        }

        if (paymentStatus is MembershipPaymentStatus.Paid or MembershipPaymentStatus.Complimentary)
        {
            return false;
        }

        return status is not ApplicationStatus.Approved
            and not ApplicationStatus.Paid
            and not ApplicationStatus.MembershipActive;
    }
}
