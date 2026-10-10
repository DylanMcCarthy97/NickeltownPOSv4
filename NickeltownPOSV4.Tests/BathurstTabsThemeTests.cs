using NickeltownPOSV4.Models;
using NickeltownPOSV4.Models.Settings;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Services.Settings;
using NickeltownPOSV4.Themes;
using Xunit;

namespace NickeltownPOSV4.Tests;

[Collection("BathurstTabsTheme")]
public sealed class BathurstTabsThemeTests
{
    private sealed class MemorySettings : IAppSettingsRepository
    {
        private readonly Dictionary<string, object> _store = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (_store.TryGetValue(key, out var o) && o is T t)
            {
                return Task.FromResult<T?>(t);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, bool isSecret = false, CancellationToken cancellationToken = default) where T : class
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSession : IUserSessionService
    {
        public bool IsSignedIn { get; set; } = true;
        public long? ActiveStaffId => 1;
        public string? ActiveStaffLegacyId => "t1";
        public string? DisplayName => "Test";
        public string? Role => IsAdmin ? "Admin" : "Bartender";
        public bool IsAdmin { get; set; } = true;
        public bool IsTreasurer => false;
        public bool IsManager => IsAdmin;
        public bool CanAccessAdmin => IsAdmin;
        public bool CanAccessReports => IsAdmin;
        public bool CanAccessTreasurer => IsAdmin;
        public bool IsDeveloper => false;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void SetSignedIn(long staffPk, string? legacyId, string displayName, string? role, bool isDeveloper = false)
        {
        }

        public void Clear()
        {
        }
    }

    [Fact]
    public async Task Default_SharedPreference_IsNormal()
    {
        AppTheme.Apply(UiThemeId.Light);
        BathurstTabsTheme.IsEnabled = false;
        var theme = new BathurstTabsThemeService(new MemorySettings(), new FakeSession());
        await theme.InitializeAsync();

        Assert.False(theme.IsEnabled);
        Assert.False(BathurstTabsTheme.IsEnabled);
    }

    [Fact]
    public async Task Admin_CanEnable_AndPersist_AcrossReload()
    {
        var settings = new MemorySettings();
        var theme = new BathurstTabsThemeService(settings, new FakeSession { IsAdmin = true });
        await theme.InitializeAsync();

        await theme.SetEnabledAsync(true);
        Assert.True(theme.IsEnabled);
        Assert.True(BathurstTabsTheme.IsEnabled);

        var again = new BathurstTabsThemeService(settings, new FakeSession { IsAdmin = false });
        await again.InitializeAsync();
        Assert.True(again.IsEnabled);
        Assert.False(again.CanToggle);
    }

    [Fact]
    public async Task NonAdmin_CannotChangePreference()
    {
        var settings = new MemorySettings();
        var admin = new BathurstTabsThemeService(settings, new FakeSession { IsAdmin = true });
        await admin.SetEnabledAsync(true);

        var nonAdmin = new BathurstTabsThemeService(settings, new FakeSession { IsAdmin = false });
        await nonAdmin.InitializeAsync();
        Assert.True(nonAdmin.IsEnabled);

        await nonAdmin.SetEnabledAsync(false);
        Assert.True(nonAdmin.IsEnabled);

        var check = await settings.GetAsync<TabsVisualThemePreferences>(TabsVisualThemePreferences.SettingsKey);
        Assert.True(check!.IsBathurstTheme);
    }

    [Fact]
    public void Appearance_Bathurst1000_MapsToLight_WithoutEnablingOverlay()
    {
        BathurstTabsTheme.IsEnabled = false;
        AppTheme.Apply(UiThemeId.Bathurst1000);

        Assert.Equal(UiThemeId.Light, AppTheme.CurrentThemeId);
        Assert.False(BathurstTabsTheme.IsEnabled);
    }

    [Fact]
    public void RecommendedThemes_DoNotIncludeBathurst1000()
    {
        var themes = new PosThemeService();
        Assert.DoesNotContain(UiThemeId.Bathurst1000, themes.RecommendedThemeIds);
    }

    [Theory]
    [InlineData(TabBalanceTier.Good, 0xFF6DE58Bu)]
    [InlineData(TabBalanceTier.Low, 0xFFFFC268u)]
    [InlineData(TabBalanceTier.Negative, 0xFFFF7575u)]
    [InlineData(TabBalanceTier.Settled, 0xFFC4CAD2u)]
    public void BalanceColors_MatchRaceDayPalette(TabBalanceTier tier, uint expected)
    {
        Assert.Equal(expected, BathurstTabsTheme.BalanceColor(tier));
    }

    [Fact]
    public void SettingsKey_IsInstallationScoped()
    {
        Assert.Equal("tabs_visual_theme.v1", BathurstTabsTheme.SettingsKey);
        Assert.Equal("tabs_visual_theme.v1", TabsVisualThemePreferences.SettingsKey);
    }

    [Fact]
    public void NegativeBalance_IsNeverGreen()
    {
        Assert.NotEqual(BathurstTabsTheme.BalanceGood, BathurstTabsTheme.BalanceColor(TabBalanceTier.Negative));
        Assert.Equal(BathurstTabsTheme.BalanceNegative, BathurstTabsTheme.BalanceColor(TabBalanceTier.Negative));
    }

    [Fact]
    public void CardBackgroundIndex_IsStable_ForSameTabId()
    {
        var a = ViewModels.TabsBoardCellViewModel.StableBackgroundIndex("tab-abc", 4);
        var b = ViewModels.TabsBoardCellViewModel.StableBackgroundIndex("tab-abc", 4);
        var c = ViewModels.TabsBoardCellViewModel.StableBackgroundIndex("tab-xyz", 4);
        Assert.Equal(a, b);
        Assert.InRange(a, 0, 3);
        Assert.InRange(c, 0, 3);
    }

    [Fact]
    public void FreeItemTile_IsDeepBlue_NotBrightPrimary()
    {
        Assert.Equal(0xFF1A2744u, BathurstTabsTheme.FreeItemTile);
        Assert.NotEqual(0xFF2F6FEDu, BathurstTabsTheme.FreeItemTile);
    }
}
