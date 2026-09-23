using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <inheritdoc />
public sealed class FfSubSyncLocator : IFfSubSyncLocator
{
    private readonly string _pluginDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfSubSyncLocator"/> class,
    /// using the directory this assembly is loaded from as the plugin directory.
    /// </summary>
    public FfSubSyncLocator()
        : this(Path.GetDirectoryName(typeof(FfSubSyncLocator).Assembly.Location) ?? AppContext.BaseDirectory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FfSubSyncLocator"/> class.
    /// </summary>
    /// <param name="pluginDirectory">Directory to look for a bundled binary under.</param>
    public FfSubSyncLocator(string pluginDirectory)
    {
        _pluginDirectory = pluginDirectory;
    }

    /// <inheritdoc />
    public string Resolve(string configuredPath)
    {
        var isSupportedPlatform = OperatingSystem.IsLinux() && RuntimeInformation.OSArchitecture == Architecture.X64;

        var bundledPath = Path.Combine(
            _pluginDirectory,
            FfSubSyncResolver.BundledRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var bundledFileExists = isSupportedPlatform && File.Exists(bundledPath);

        var resolved = FfSubSyncResolver.Resolve(configuredPath, _pluginDirectory, bundledFileExists, isSupportedPlatform);

        if (bundledFileExists && resolved == bundledPath)
        {
            EnsureExecutable(bundledPath);
        }

        return resolved;
    }

    private static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        try
        {
            const UnixFileMode ExecuteBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            var mode = File.GetUnixFileMode(path);
            if ((mode & ExecuteBits) != ExecuteBits)
            {
                File.SetUnixFileMode(path, mode | ExecuteBits);
            }
        }
        catch (IOException)
        {
            // Best-effort; if this fails, the subsequent process launch will
            // surface a clear "permission denied" error instead.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
