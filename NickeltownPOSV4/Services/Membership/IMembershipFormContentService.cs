using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public interface IMembershipFormContentService
{
    Task<IReadOnlyList<MembershipFormContentSection>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MembershipFormContentSection?> GetByKeyAsync(string sectionKey, CancellationToken cancellationToken = default);

    Task<string?> GetBodyAsync(string sectionKey, CancellationToken cancellationToken = default);
}
