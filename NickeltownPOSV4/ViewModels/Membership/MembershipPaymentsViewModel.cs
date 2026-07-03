using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipPaymentsViewModel : MembershipSectionShellViewModel
{
    public MembershipPaymentsViewModel(INavigationService navigation)
        : base(
            navigation,
            "Payments",
            "Application and renewal payment tracking.",
            "No membership payments recorded yet.")
    {
    }
}
