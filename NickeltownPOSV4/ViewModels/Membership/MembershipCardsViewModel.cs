using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipCardsViewModel : MembershipSectionShellViewModel
{
    public MembershipCardsViewModel(INavigationService navigation)
        : base(
            navigation,
            "Membership Cards",
            "Track membership card issuance.",
            "No membership cards issued yet.")
    {
    }
}
