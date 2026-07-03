using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipReportsViewModel : MembershipSectionShellViewModel
{
    public MembershipReportsViewModel(INavigationService navigation)
        : base(
            navigation,
            "Reports",
            "Membership reporting and exports.",
            "No membership reports available yet.")
    {
    }
}
