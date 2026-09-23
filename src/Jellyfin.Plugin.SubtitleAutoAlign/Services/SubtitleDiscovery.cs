using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// Finds the external subtitle files Jellyfin knows about for a video.
/// </summary>
public static class SubtitleDiscovery
{
    /// <summary>
    /// Gets the paths of all external subtitle streams of <paramref name="video"/>.
    /// </summary>
    /// <param name="video">The video item.</param>
    /// <returns>Subtitle file paths, including already-aligned outputs.</returns>
    public static IReadOnlyList<string> GetExternalSubtitlePaths(BaseItem video)
    {
        return video.GetMediaStreams()
            .Where(stream => stream.Type == MediaStreamType.Subtitle
                && stream.IsExternal
                && !string.IsNullOrEmpty(stream.Path))
            .Select(stream => stream.Path)
            .ToList();
    }
}
