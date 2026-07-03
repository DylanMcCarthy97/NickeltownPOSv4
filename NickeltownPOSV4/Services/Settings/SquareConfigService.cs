using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;

namespace NickeltownPOSV4.Services.Settings;

public sealed class SquareConfigService : ISquareConfigService
{
    /// <summary>Same key already populated by the migration import path (<c>SqliteSquareConfigRepository</c>).</summary>
    public const string SettingsKey = "square_config.v4";

    private readonly IAppSettingsRepository _settings;

    public SquareConfigService(IAppSettingsRepository settings) => _settings = settings;

    public async Task<AppSquareConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _settings.GetAsync<AppSquareConfig>(SettingsKey, cancellationToken).ConfigureAwait(false);
        return existing ?? new AppSquareConfig();
    }

    public Task SaveAsync(AppSquareConfig config, CancellationToken cancellationToken = default) =>
        _settings.SetAsync(SettingsKey, config, isSecret: true, cancellationToken);
}
