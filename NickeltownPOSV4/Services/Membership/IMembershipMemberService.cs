using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public interface IMembershipMemberService
{
    Task<IReadOnlyList<MembershipMemberListItem>> ListAsync(CancellationToken cancellationToken = default);
}
