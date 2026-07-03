using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Data.Sqlite;

public interface IMembershipSettingsRepository
{
    Task<MembershipSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(MembershipSettings settings, CancellationToken cancellationToken = default);
}
