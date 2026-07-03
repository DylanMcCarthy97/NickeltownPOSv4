using System;
using System.Threading;
using System.Threading.Tasks;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Settings;

namespace NickeltownPOSV4.Services.Pitstop;

/// <summary>Loads Pitstop card surcharge % from Square config, then app preferences, then default.</summary>
public sealed class PitstopSurchargeConfigLoader
{
    public const string PitstopPosPreferencesKey = "pitstop_pos_preferences.v1";

    private const decimal DefaultPercent = 1.7m;

    private readonly ISquareConfigService _squareConfig;
    private readonly IAppSettingsRepository _appSettings;

    public PitstopSurchargeConfigLoader(
        ISquareConfigService squareConfig,
        IAppSettingsRepository appSettings)
    {
        _squareConfig = squareConfig;
        _appSettings = appSettings;
    }

    public async Task<decimal> LoadCardSurchargePercentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var sq = await _squareConfig.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (sq.PitstopTerminalCardSurchargePercent is > 0 and < 100)
            {
                return decimal.Round(sq.PitstopTerminalCardSurchargePercent, 2, MidpointRounding.AwayFromZero);
            }

            var p = await _appSettings
                .GetAsync<PitstopPosPreferences>(PitstopPosPreferencesKey, cancellationToken)
                .ConfigureAwait(false);
            if (p?.CardSurchargePercent is > 0 and < 100)
            {
                return decimal.Round(p.CardSurchargePercent, 2, MidpointRounding.AwayFromZero);
            }

            return DefaultPercent;
        }
        catch
        {
            return DefaultPercent;
        }
    }
}
