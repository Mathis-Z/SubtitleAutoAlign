using System.IO;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class FfSubSyncResolverTests
{
    [Fact]
    public void Resolve_ExplicitOverride_WinsOverBundledAndAutoDetect()
    {
        var result = FfSubSyncResolver.Resolve(
            configuredPath: "/custom/ffsubsync",
            pluginDirectory: "/plugin",
            bundledFileExists: true,
            isSupportedPlatform: true);

        Assert.Equal("/custom/ffsubsync", result);
    }

    [Fact]
    public void Resolve_AutoDetect_SupportedPlatformWithBundle_ReturnsBundledPath()
    {
        var result = FfSubSyncResolver.Resolve(
            configuredPath: string.Empty,
            pluginDirectory: "/plugin",
            bundledFileExists: true,
            isSupportedPlatform: true);

        Assert.Equal(Path.Combine("/plugin", "bundled", "linux-x64", "ffsubsync"), result);
    }

    [Fact]
    public void Resolve_AutoDetect_UnsupportedPlatform_FallsBackToPath()
    {
        var result = FfSubSyncResolver.Resolve(
            configuredPath: string.Empty,
            pluginDirectory: "/plugin",
            bundledFileExists: false,
            isSupportedPlatform: false);

        Assert.Equal("ffsubsync", result);
    }

    [Fact]
    public void Resolve_AutoDetect_SupportedPlatformNoBundle_FallsBackToPath()
    {
        var result = FfSubSyncResolver.Resolve(
            configuredPath: string.Empty,
            pluginDirectory: "/plugin",
            bundledFileExists: false,
            isSupportedPlatform: true);

        Assert.Equal("ffsubsync", result);
    }

    [Fact]
    public void Resolve_WhitespaceConfiguredPath_TreatedAsAutoDetect()
    {
        var result = FfSubSyncResolver.Resolve(
            configuredPath: "   ",
            pluginDirectory: "/plugin",
            bundledFileExists: false,
            isSupportedPlatform: true);

        Assert.Equal("ffsubsync", result);
    }
}
