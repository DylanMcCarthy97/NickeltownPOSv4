using NickeltownPOSV4.Services.Updates;
using Xunit;

namespace NickeltownPOSV4.Tests;

public sealed class AppUpdateServiceTests
{
    [Theory]
    [InlineData(
        "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed",
        "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed")]
    [InlineData(
        "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed/",
        "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed")]
    [InlineData(
        "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed/update-manifest.json",
        "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed")]
    [InlineData(
        "https://raw.githubusercontent.com/DylanMcCarthy97/NickeltownPOSv4/main/deploy/update-manifest.json?nocache=1",
        "https://raw.githubusercontent.com/DylanMcCarthy97/NickeltownPOSv4/main/deploy")]
    public void NormalizeFeedBase_StripsManifestFileAndQuery(string input, string expected)
    {
        Assert.Equal(expected, AppUpdateService.NormalizeFeedBase(input));
    }

    [Fact]
    public void BuildManifestPath_AppendsNocacheStampForHttpFeeds()
    {
        var path = AppUpdateService.BuildManifestPath(
            "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed/update-manifest.json");

        Assert.StartsWith(
            "https://github.com/DylanMcCarthy97/NickeltownPOSv4/releases/download/update-feed/update-manifest.json?nocache=",
            path);
    }
}
