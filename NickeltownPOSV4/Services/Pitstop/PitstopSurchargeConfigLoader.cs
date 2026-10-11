using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>
/// Card surcharge percent for new Square charges. Always 0 — Australian card
/// payments cannot include a customer surcharge.
/// </summary>
public sealed class PitstopSurchargeConfigLoader
{
    public const string PitstopPosPreferencesKey = "pitstop_pos_preferences.v1";

    public PitstopSurchargeConfigLoader(
        ISquareConfigService squareConfig,
        IAppSettingsRepository appSettings)
    {
        _ = squareConfig;
        _ = appSettings;
    }

    public Task<decimal> LoadCardSurchargePercentAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(0m);
    }
}
