using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;

namespace NickeltownPOSV4.Services.Settings;

public sealed class EmailConfigService : IEmailConfigService
{
    public const string SettingsKey = "email_config.v4";

    private readonly IAppSettingsRepository _settings;

    public EmailConfigService(IAppSettingsRepository settings) => _settings = settings;

    public async Task<AppEmailConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _settings.GetAsync<AppEmailConfig>(SettingsKey, cancellationToken).ConfigureAwait(false);
        return existing ?? new AppEmailConfig();
    }

    public Task SaveAsync(AppEmailConfig config, CancellationToken cancellationToken = default) =>
        _settings.SetAsync(SettingsKey, config, isSecret: true, cancellationToken);
}
