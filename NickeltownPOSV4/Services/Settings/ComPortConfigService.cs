using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NickeltownPOSV4.Models.Settings;

namespace NickeltownPOSV4.Services.Settings;

public sealed class ComPortConfigService : IComPortConfigService
{
    public const string SettingsKey = "comport_config.v4";

    private static readonly int[] CommonBauds = [9600, 19200, 38400, 57600, 115200];

    private readonly IAppSettingsRepository _settings;
    private readonly ILogger<ComPortConfigService> _logger;

    public ComPortConfigService(IAppSettingsRepository settings, ILogger<ComPortConfigService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<AppComPortConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _settings.GetAsync<AppComPortConfig>(SettingsKey, cancellationToken).ConfigureAwait(false);
        return existing ?? new AppComPortConfig();
    }

    public Task SaveAsync(AppComPortConfig config, CancellationToken cancellationToken = default) =>
        _settings.SetAsync(SettingsKey, config, isSecret: false, cancellationToken);

    public IReadOnlyList<string> ListAvailablePorts()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Serial port enumeration failed.");
            return Array.Empty<string>();
        }
    }

    public IReadOnlyList<int> GetCommonBaudRates() => CommonBauds;
}
