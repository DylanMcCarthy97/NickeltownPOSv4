using NickeltownPOSV4.Services;

namespace NickeltownPOSV4.ViewModels.Membership;

public class MembershipSectionShellViewModel : MembershipSubViewModelBase
{
    public MembershipSectionShellViewModel(
        INavigationService navigation,
        string title,
        string subtitle,
        string emptyStateMessage)
        : base(navigation)
    {
        Title = title;
        Subtitle = subtitle;
        EmptyStateMessage = emptyStateMessage;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string EmptyStateMessage { get; }
}
