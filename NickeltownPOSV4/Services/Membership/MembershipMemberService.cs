using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Data.Sqlite;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public sealed class MembershipMemberService : IMembershipMemberService
{
    private readonly IMembershipMemberRepository _members;

    public MembershipMemberService(IMembershipMemberRepository members) => _members = members;

    public Task<IReadOnlyList<MembershipMemberListItem>> ListAsync(CancellationToken cancellationToken = default) =>
        _members.ListAsync(cancellationToken);
}
