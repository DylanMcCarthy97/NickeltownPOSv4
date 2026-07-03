using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Membership;

namespace NickeltownPOSV4.Services.Membership;

public interface IMembershipSettingsService
{
    Task<MembershipSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(MembershipSettings settings, CancellationToken cancellationToken = default);
}
