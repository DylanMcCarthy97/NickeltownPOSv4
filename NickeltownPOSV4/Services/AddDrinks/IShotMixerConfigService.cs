using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.AddDrinks;

public interface IShotMixerConfigService
{
    void Invalidate();

    Task EnsureExistsAsync(CancellationToken cancellationToken = default);

    Task<ShotMixerRuntimeConfig> GetAsync(bool isGuestTab, CancellationToken cancellationToken = default);
}
