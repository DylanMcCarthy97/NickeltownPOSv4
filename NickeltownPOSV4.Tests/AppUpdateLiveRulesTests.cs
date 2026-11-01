using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Services.Updates;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class AppUpdateLiveRulesTests
{
    [Fact]
    public void NewConfig_UpdatesItself()
    {
        var cfg = new AppUpdateConfig();
        Assert.True(cfg.CheckOnStartup);
        Assert.True(cfg.AutoInstall);
        Assert.True(AppUpdateLiveRules.ShouldKeepLooking(cfg.CheckOnStartup));
    }

    [Fact]
    public void InstallsOnlyOnTheLoginScreen()
    {
        Assert.True(AppUpdateLiveRules.CanInstallNow(autoInstall: true, signedIn: false));
        Assert.False(AppUpdateLiveRules.CanInstallNow(autoInstall: true, signedIn: true));
        Assert.False(AppUpdateLiveRules.CanInstallNow(autoInstall: false, signedIn: false));
    }

    [Fact]
    public void CanTurnLookingOff()
    {
        Assert.False(AppUpdateLiveRules.ShouldKeepLooking(checkOnStartup: false));
    }

    [Fact]
    public async Task Load_TurnsOnSelfUpdateForOlderTillsOnce()
    {
        var store = new MemorySettings();
        await store.SetAsync(
            AppUpdateConfigService.SettingsKey,
            new AppUpdateConfig { CheckOnStartup = false, AutoInstall = false, LiveDefaultsApplied = false });
        var service = new AppUpdateConfigService(store);

        var loaded = await service.LoadAsync();
        Assert.True(loaded.CheckOnStartup);
        Assert.True(loaded.AutoInstall);
        Assert.True(loaded.LiveDefaultsApplied);

        loaded.AutoInstall = false;
        await service.SaveAsync(loaded);
        var later = await service.LoadAsync();
        Assert.False(later.AutoInstall);
    }

    private sealed class MemorySettings : IAppSettingsRepository
    {
        private readonly Dictionary<string, object> _items = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (_items.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, bool isSecret = false, CancellationToken cancellationToken = default) where T : class
        {
            _items[key] = value!;
            return Task.CompletedTask;
        }
    }
}
