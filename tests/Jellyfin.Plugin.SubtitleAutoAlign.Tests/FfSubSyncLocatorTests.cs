using System;
using System.IO;
using System.Runtime.InteropServices;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class FfSubSyncLocatorTests : IDisposable
{
    private static readonly bool IsSupportedPlatform =
        OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64;

    private readonly string _pluginDirectory;

    public FfSubSyncLocatorTests()
    {
        _pluginDirectory = Directory.CreateTempSubdirectory("ffsubsync-locator-tests-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_pluginDirectory, recursive: true);
    }

    [Fact]
    public void Resolve_ExplicitOverride_IsReturnedVerbatim()
    {
        var locator = new FfSubSyncLocator(_pluginDirectory);

        var result = locator.Resolve("/opt/ffsubsync/ffsubsync");

        Assert.Equal("/opt/ffsubsync/ffsubsync", result);
    }

    [Fact]
    public void Resolve_NoBundledBinary_FallsBackToPath()
    {
        var locator = new FfSubSyncLocator(_pluginDirectory);

        var result = locator.Resolve(string.Empty);

        Assert.Equal("ffsubsync", result);
    }

    [Fact]
    public void Resolve_BundledBinaryPresent_ReturnsBundledPath()
    {
        if (!IsSupportedPlatform)
        {
            // Bundled binaries are only shipped for linux-x64; on other
            // platforms auto-detect always falls back to PATH, which is
            // already covered by Resolve_NoBundledBinary_FallsBackToPath.
            return;
        }

        var bundledDir = Path.Combine(_pluginDirectory, "bundled", "linux-x64");
        Directory.CreateDirectory(bundledDir);
        var bundledPath = Path.Combine(bundledDir, "ffsubsync");
        File.WriteAllText(bundledPath, "#!/bin/sh\nexit 0\n");

        var locator = new FfSubSyncLocator(_pluginDirectory);

        var result = locator.Resolve(string.Empty);

        Assert.Equal(bundledPath, result);
        Assert.True((File.GetUnixFileMode(bundledPath) & UnixFileMode.UserExecute) != 0);
    }
}
