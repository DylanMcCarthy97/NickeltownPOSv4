using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipMembersViewModel : MembershipSectionShellViewModel
{
    public MembershipMembersViewModel(INavigationService navigation)
        : base(
            navigation,
            "Members",
            "Active club members for the current membership year.",
            "No members recorded yet. Approved applications will create member records here.")
    {
    }
}
