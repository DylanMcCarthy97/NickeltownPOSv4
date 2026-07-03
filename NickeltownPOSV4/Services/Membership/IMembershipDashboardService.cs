using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public sealed class MembershipDashboardSummary
{
    public int TotalApplications { get; init; }

    public int PendingApplications { get; init; }

    public int ActiveMembers { get; init; }

    public int RenewalsDue { get; init; }

    public string MembershipYearLabel { get; init; } = string.Empty;
}

public interface IMembershipDashboardService
{
    Task<MembershipDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
