using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Data.Sqlite;

public interface IMembershipMemberRepository
{
    Task<IReadOnlyList<MembershipMemberListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<MembershipMember?> GetByApplicationIdAsync(long applicationId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForApplicationAsync(long applicationId, CancellationToken cancellationToken = default);

    Task UpsertFromApplicationAsync(MembershipApplication application, string membershipYearLabel, CancellationToken cancellationToken = default);

    Task<int> CountActiveAsync(CancellationToken cancellationToken = default);

    Task<int> CountExpiringWithinDaysAsync(int days, CancellationToken cancellationToken = default);
}
