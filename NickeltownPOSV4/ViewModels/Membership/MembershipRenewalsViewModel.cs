using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipRenewalsViewModel : MembershipSectionShellViewModel
{
    public MembershipRenewalsViewModel(INavigationService navigation)
        : base(
            navigation,
            "Renewals",
            "Memberships approaching expiry based on reminder settings.",
            "No renewals due. Members nearing expiry will appear here.")
    {
    }
}
