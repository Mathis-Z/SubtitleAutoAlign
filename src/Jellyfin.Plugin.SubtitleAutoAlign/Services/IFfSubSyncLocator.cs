namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// Resolves the ffsubsync executable to invoke, accounting for a bundled
/// binary shipped alongside the plugin and the user's configuration override.
/// </summary>
public interface IFfSubSyncLocator
{
    /// <summary>
    /// Resolves the ffsubsync executable path or name to invoke.
    /// </summary>
    /// <param name="configuredPath">The user-configured <c>FfSubSyncPath</c>; empty means auto-detect.</param>
    /// <returns>An absolute path to a binary, or a bare executable name to resolve via PATH.</returns>
    string Resolve(string configuredPath);
}
