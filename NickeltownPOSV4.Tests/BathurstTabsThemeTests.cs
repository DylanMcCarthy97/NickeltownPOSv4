using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Themes;
using Xunit;

namespace NickeltownPOSV4.Tests;

[Collection("BathurstTabsTheme")]
public sealed class BathurstTabsThemeTests
{
    [Fact]
    public void Default_IsNormalTheme()
    {
        var theme = new BathurstTabsThemeService(new MemorySettings());
        Assert.False(theme.IsEnabled);
        Assert.False(BathurstTabsTheme.IsEnabled);
    }

    [Fact]
    public async Task Enable_Persists_AndReloads()
    {
        var store = new MemorySettings();
        var theme = new BathurstTabsThemeService(store);

        await theme.SetEnabledAsync(true);

        Assert.True(theme.IsEnabled);
        Assert.True(BathurstTabsTheme.IsEnabled);

        var reloaded = new BathurstTabsThemeService(store);
        Assert.True(reloaded.IsEnabled);
        Assert.True(BathurstTabsTheme.IsEnabled);
    }

    [Fact]
    public async Task Disable_AfterRaceDay_RestoresNormalDefault()
    {
        var store = new MemorySettings();
        await store.SetAsync(BathurstTabsTheme.SettingsKey, new BathurstTabsThemeSettings { Enabled = true });
        var theme = new BathurstTabsThemeService(store);
        Assert.True(theme.IsEnabled);

        await theme.SetEnabledAsync(false);

        Assert.False(theme.IsEnabled);
        Assert.False(BathurstTabsTheme.IsEnabled);
        var reloaded = new BathurstTabsThemeService(store);
        Assert.False(reloaded.IsEnabled);
    }

    [Theory]
    [InlineData(TabBalanceTier.Good, 0xFF22C55Eu)]
    [InlineData(TabBalanceTier.Low, 0xFFF59E0Bu)]
    [InlineData(TabBalanceTier.Negative, 0xFFEF4444u)]
    [InlineData(TabBalanceTier.Settled, 0xFF94A3B8u)]
    public void BalanceColors_StayHighContrast(TabBalanceTier tier, uint expected)
    {
        Assert.Equal(expected, BathurstTabsTheme.BalanceColor(tier));
    }

    [Fact]
    public void GuestStrip_UsesGuestPurple()
    {
        Assert.Equal(0xFFA855F7u, BathurstTabsTheme.StripColor(isGuest: true, TabBalanceTier.Good));
        Assert.Equal(BathurstTabsTheme.BalanceGood, BathurstTabsTheme.StripColor(isGuest: false, TabBalanceTier.Good));
    }

    [Fact]
    public async Task Reload_PicksUpExternalSettingChange()
    {
        var store = new MemorySettings();
        var theme = new BathurstTabsThemeService(store);
        Assert.False(theme.IsEnabled);

        await store.SetAsync(BathurstTabsTheme.SettingsKey, new BathurstTabsThemeSettings { Enabled = true });
        await theme.ReloadAsync();

        Assert.True(theme.IsEnabled);
        Assert.True(BathurstTabsTheme.IsEnabled);
    }

    [Fact]
    public void SettingsKey_DoesNotRequireSchemaChange()
    {
        Assert.Equal("ui.bathurst_tabs_theme.v1", BathurstTabsTheme.SettingsKey);
        Assert.Contains("11 OCTOBER 2026", BathurstTabsTheme.RaceDayEdition, StringComparison.Ordinal);
    }

    private sealed class MemorySettings : IAppSettingsRepository
    {
        private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_store.TryGetValue(key, out var value) ? (T)value : null);

        public Task SetAsync<T>(string key, T value, bool isSecret = false, CancellationToken cancellationToken = default)
            where T : class
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }
}
