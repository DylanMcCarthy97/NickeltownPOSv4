using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Pitstop;
using NickeltownPOSV4.Services.Settings;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class PitstopSurchargeConfigLoaderTests
{
    private sealed class FixedSquareConfig : ISquareConfigService
    {
        public AppSquareConfig Config { get; set; } = new() { PitstopTerminalCardSurchargePercent = 1.7m };

        public Task<AppSquareConfig> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Config);

        public Task SaveAsync(AppSquareConfig config, CancellationToken cancellationToken = default)
        {
            Config = config;
            return Task.CompletedTask;
        }
    }

    private sealed class MemorySettings : IAppSettingsRepository
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
            Task.FromResult<T?>(new PitstopPosPreferences { CardSurchargePercent = 1.7m } as T);

        public Task SetAsync<T>(string key, T value, bool isSecret = false, CancellationToken cancellationToken = default) where T : class =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task LoadCardSurchargePercent_AlwaysZero_EvenWhenLegacyConfigHasRate()
    {
        var loader = new PitstopSurchargeConfigLoader(new FixedSquareConfig(), new MemorySettings());
        var percent = await loader.LoadCardSurchargePercentAsync();
        Assert.Equal(0m, percent);
    }
}
