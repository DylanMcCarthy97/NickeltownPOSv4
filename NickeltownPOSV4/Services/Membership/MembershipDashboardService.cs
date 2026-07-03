using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public sealed class MembershipDashboardService : IMembershipDashboardService
{
    private readonly IMembershipApplicationRepository _applications;
    private readonly IMembershipMemberRepository _members;
    private readonly IMembershipSettingsRepository _settings;

    public MembershipDashboardService(
        IMembershipApplicationRepository applications,
        IMembershipMemberRepository members,
        IMembershipSettingsRepository settings)
    {
        _applications = applications;
        _members = members;
        _settings = settings;
    }

    public async Task<MembershipDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        var totalApplications = await _applications.CountAsync(cancellationToken).ConfigureAwait(false);
        var pending = await _applications.CountByStatusAsync(ApplicationStatus.PendingReview, cancellationToken).ConfigureAwait(false);
        pending += await _applications.CountByStatusAsync(ApplicationStatus.CommitteeReview, cancellationToken).ConfigureAwait(false);
        pending += await _applications.CountByStatusAsync(ApplicationStatus.Submitted, cancellationToken).ConfigureAwait(false);
        var activeMembers = await _members.CountActiveAsync(cancellationToken).ConfigureAwait(false);
        var renewalsDue = await _members.CountExpiringWithinDaysAsync(settings.ReminderDaysBeforeExpiry, cancellationToken).ConfigureAwait(false);

        return new MembershipDashboardSummary
        {
            TotalApplications = totalApplications,
            PendingApplications = pending,
            ActiveMembers = activeMembers,
            RenewalsDue = renewalsDue,
            MembershipYearLabel = settings.MembershipYearLabel,
        };
    }
}
