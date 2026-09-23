using System;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Models;

/// <summary>
/// A request to align a single subtitle file against its movie's video file.
/// </summary>
/// <param name="ItemId">Jellyfin library item id, for logging/correlation.</param>
/// <param name="VideoPath">Path to the reference video file.</param>
/// <param name="SubtitlePath">Path to the subtitle file to align.</param>
public sealed record AlignmentRequest(Guid ItemId, string VideoPath, string SubtitlePath);
