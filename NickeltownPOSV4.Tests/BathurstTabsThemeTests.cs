using NickeltownPOSV4.Models;
using NickeltownPOSV4.Services;
using NickeltownPOSV4.Themes;
using Xunit;

namespace NickeltownPOSV4.Tests;

[Collection("BathurstTabsTheme")]
public sealed class BathurstTabsThemeTests
{
    [Fact]
    public void Default_Appearance_IsNotBathurst()
    {
        AppTheme.Apply(UiThemeId.Light);
        var theme = new BathurstTabsThemeService();

        Assert.Equal(UiThemeId.Light, AppTheme.CurrentThemeId);
        Assert.False(theme.IsEnabled);
        Assert.False(BathurstTabsTheme.IsEnabled);
    }

    [Fact]
    public void Appearances_Bathurst1000_EnablesRaceDayOverlay()
    {
        AppTheme.Apply(UiThemeId.Light);
        Assert.False(BathurstTabsTheme.IsEnabled);

        AppTheme.Apply(UiThemeId.Bathurst1000);

        Assert.True(BathurstTabsTheme.IsEnabled);
        Assert.Equal(UiThemeId.Bathurst1000, AppTheme.CurrentThemeId);

        AppTheme.Apply(UiThemeId.Light);
        Assert.False(BathurstTabsTheme.IsEnabled);
    }

    [Fact]
    public void Appearances_OtherTheme_TurnsOverlayOff()
    {
        AppTheme.Apply(UiThemeId.Bathurst1000);
        Assert.True(BathurstTabsTheme.IsEnabled);

        AppTheme.Apply(UiThemeId.PitstopDark);

        Assert.False(BathurstTabsTheme.IsEnabled);
        Assert.Equal(UiThemeId.PitstopDark, AppTheme.CurrentThemeId);
    }

    [Fact]
    public void RecommendedThemes_IncludeBathurst1000()
    {
        var themes = new PosThemeService();

        Assert.Contains(UiThemeId.Bathurst1000, themes.RecommendedThemeIds);
        Assert.Contains("Bathurst", themes.GetDisplayName(UiThemeId.Bathurst1000), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiThemeId.Bathurst1000, themes.RecommendedThemeIds[0]);
    }

    [Fact]
    public async Task Reload_FollowsCurrentAppearance()
    {
        AppTheme.Apply(UiThemeId.Light);
        var theme = new BathurstTabsThemeService();
        Assert.False(theme.IsEnabled);

        AppTheme.Apply(UiThemeId.Bathurst1000);
        await theme.ReloadAsync();

        Assert.True(theme.IsEnabled);
        Assert.True(BathurstTabsTheme.IsEnabled);
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
    public void SettingsKey_DoesNotRequireSchemaChange()
    {
        Assert.Equal("ui.bathurst_tabs_theme.v1", BathurstTabsTheme.SettingsKey);
        Assert.Contains("11 OCTOBER 2026", BathurstTabsTheme.RaceDayEdition, StringComparison.Ordinal);
    }
}
