using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;

namespace NickeltownPOSV4.Services.Settings;

public interface IComPortConfigService
{
    Task<AppComPortConfig> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppComPortConfig config, CancellationToken cancellationToken = default);

    IReadOnlyList<string> ListAvailablePorts();

    IReadOnlyList<int> GetCommonBaudRates();
}
