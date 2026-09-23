using System.IO;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// Pure logic for deciding which ffsubsync executable to invoke: an explicit
/// user override, a bundled platform-specific binary shipped alongside the
/// plugin, or a bare "ffsubsync" resolved via PATH.
/// </summary>
public static class FfSubSyncResolver
{
    /// <summary>
    /// Path, relative to the plugin's own directory, at which a bundled
    /// ffsubsync binary is expected for Linux x64 (the only platform CI
    /// currently builds a bundled binary for).
    /// </summary>
    public const string BundledRelativePath = "bundled/linux-x64/ffsubsync";

    /// <summary>
    /// Resolves the ffsubsync executable to invoke.
    /// </summary>
    /// <param name="configuredPath">The user-configured <c>FfSubSyncPath</c>; empty means auto-detect.</param>
    /// <param name="pluginDirectory">Directory the plugin assembly is loaded from.</param>
    /// <param name="bundledFileExists">Whether a bundled binary file exists at the expected location.</param>
    /// <param name="isSupportedPlatform">Whether the current OS/architecture has a bundled binary at all.</param>
    /// <returns>The absolute path to a bundled binary, the user's override, or the literal "ffsubsync".</returns>
    public static string Resolve(
        string configuredPath,
        string pluginDirectory,
        bool bundledFileExists,
        bool isSupportedPlatform)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        if (isSupportedPlatform && bundledFileExists)
        {
            return Path.Combine(pluginDirectory, BundledRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        return "ffsubsync";
    }
}
