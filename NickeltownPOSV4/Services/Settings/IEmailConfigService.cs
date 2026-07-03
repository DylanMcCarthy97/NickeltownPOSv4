using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;

namespace NickeltownPOSV4.Services.Settings;

public interface IEmailConfigService
{
    Task<AppEmailConfig> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppEmailConfig config, CancellationToken cancellationToken = default);
}
