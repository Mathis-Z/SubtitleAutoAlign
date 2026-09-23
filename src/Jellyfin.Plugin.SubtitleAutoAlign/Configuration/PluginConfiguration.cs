using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Configuration;

/// <summary>
/// Configuration for the Subtitle Auto Align plugin.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether newly-appeared subtitles are
    /// aligned automatically. Does not affect the "Align all subtitles" task.
    /// </summary>
    public bool EnableAutoAlign { get; set; } = true;

    /// <summary>
    /// Gets or sets the ffsubsync executable name or path. Empty (the default)
    /// means auto-detect: prefer the bundled ffsubsync binary for the current
    /// platform if present, otherwise fall back to "ffsubsync" on PATH. Set
    /// explicitly to override auto-detection.
    /// </summary>
    public string FfSubSyncPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets extra CLI arguments appended to every ffsubsync invocation,
    /// e.g. "--max-offset-seconds 60".
    /// </summary>
    public string ExtraArguments { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum time, in seconds, to allow a single ffsubsync
    /// invocation to run before it is killed.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Gets or sets a value indicating whether an existing aligned subtitle
    /// should be overwritten if alignment runs again for the same input.
    /// </summary>
    public bool OverwriteExistingAlignedSubtitle { get; set; }
}
