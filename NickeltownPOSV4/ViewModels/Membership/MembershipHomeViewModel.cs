using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Views.Membership;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipHomeViewModel
{
    private readonly INavigationService _navigation;

    public MembershipHomeViewModel(INavigationService navigation)
    {
        _navigation = navigation;
        OpenDashboardCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipDashboardPage)));
        OpenApplicationsCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipApplicationsPage)));
        OpenMembersCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipMembersPage)));
        OpenRenewalsCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipRenewalsPage)));
        OpenPaymentsCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipPaymentsPage)));
        OpenCardsCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipCardsPage)));
        OpenDocumentsCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipDocumentsPage)));
        OpenReportsCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipReportsPage)));
        OpenSettingsCommand = new RelayCommand(() => _navigation.Navigate(typeof(MembershipSettingsPage)));
    }

    public IRelayCommand OpenDashboardCommand { get; }

    public IRelayCommand OpenApplicationsCommand { get; }

    public IRelayCommand OpenMembersCommand { get; }

    public IRelayCommand OpenRenewalsCommand { get; }

    public IRelayCommand OpenPaymentsCommand { get; }

    public IRelayCommand OpenCardsCommand { get; }

    public IRelayCommand OpenDocumentsCommand { get; }

    public IRelayCommand OpenReportsCommand { get; }

    public IRelayCommand OpenSettingsCommand { get; }
}
