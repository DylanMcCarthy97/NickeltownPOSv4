using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipDocumentsViewModel : MembershipSectionShellViewModel
{
    public MembershipDocumentsViewModel(INavigationService navigation)
        : base(
            navigation,
            "Documents",
            "Membership forms and generated documents.",
            "No documents generated yet.")
    {
    }
}
