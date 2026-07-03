using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Membership;

namespace NickeltownPOSV4.ViewModels.Membership;

public sealed class MembershipDashboardViewModel : MembershipSubViewModelBase
{
    private readonly IMembershipDashboardService _dashboard;

    private string _membershipYearLabel = string.Empty;

    private string _totalApplicationsText = "0";

    private string _pendingApplicationsText = "0";

    private string _activeMembersText = "0";

    private string _renewalsDueText = "0";

    public MembershipDashboardViewModel(INavigationService navigation, IMembershipDashboardService dashboard)
        : base(navigation)
    {
        _dashboard = dashboard;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
    }

    public IAsyncRelayCommand LoadCommand { get; }

    public string MembershipYearLabel
    {
        get => _membershipYearLabel;
        private set => SetProperty(ref _membershipYearLabel, value);
    }

    public string TotalApplicationsText
    {
        get => _totalApplicationsText;
        private set => SetProperty(ref _totalApplicationsText, value);
    }

    public string PendingApplicationsText
    {
        get => _pendingApplicationsText;
        private set => SetProperty(ref _pendingApplicationsText, value);
    }

    public string ActiveMembersText
    {
        get => _activeMembersText;
        private set => SetProperty(ref _activeMembersText, value);
    }

    public string RenewalsDueText
    {
        get => _renewalsDueText;
        private set => SetProperty(ref _renewalsDueText, value);
    }

    public async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var summary = await _dashboard.GetSummaryAsync().ConfigureAwait(true);
            MembershipYearLabel = summary.MembershipYearLabel;
            TotalApplicationsText = summary.TotalApplications.ToString();
            PendingApplicationsText = summary.PendingApplications.ToString();
            ActiveMembersText = summary.ActiveMembers.ToString();
            RenewalsDueText = summary.RenewalsDue.ToString();
            SetStatus($"Dashboard updated {DateTime.Now:t}.");
        }
        catch (Exception ex)
        {
            SetStatus($"Load failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
