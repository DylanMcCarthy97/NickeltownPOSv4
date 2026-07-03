using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;

namespace NickeltownPOSV4.Services.Settings;

public interface ISquareConfigService
{
    Task<AppSquareConfig> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSquareConfig config, CancellationToken cancellationToken = default);
}
