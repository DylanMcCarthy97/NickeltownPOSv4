using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;

namespace NickeltownPOSV4.Services.Settings;

public interface IAppUpdateConfigService
{
    Task<AppUpdateConfig> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppUpdateConfig config, CancellationToken cancellationToken = default);
}

public sealed class AppUpdateConfigService : IAppUpdateConfigService
{
    public const string SettingsKey = "update_config.v4";

    private readonly IAppSettingsRepository _settings;

    public AppUpdateConfigService(IAppSettingsRepository settings) => _settings = settings;

    public async Task<AppUpdateConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _settings.GetAsync<AppUpdateConfig>(SettingsKey, cancellationToken).ConfigureAwait(false);
        return existing ?? new AppUpdateConfig();
    }

    public Task SaveAsync(AppUpdateConfig config, CancellationToken cancellationToken = default) =>
        _settings.SetAsync(SettingsKey, config, isSecret: false, cancellationToken);
}
