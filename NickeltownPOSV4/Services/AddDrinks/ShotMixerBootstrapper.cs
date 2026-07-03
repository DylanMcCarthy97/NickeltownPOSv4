using System.Threading;
using System.Threading.Tasks;

namespace NickeltownPOSV4.Services.AddDrinks;

public interface IShotMixerBootstrapper
{
    Task EnsureAsync(CancellationToken cancellationToken = default);
}

public sealed class ShotMixerBootstrapper : IShotMixerBootstrapper
{
    private readonly IShotMixerConfigService _config;

    public ShotMixerBootstrapper(IShotMixerConfigService config) => _config = config;

    public Task EnsureAsync(CancellationToken cancellationToken = default) =>
        _config.EnsureExistsAsync(cancellationToken);
}
