using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// A subtitle file and the video it belongs to.
/// </summary>
/// <param name="VideoPath">Path of the video.</param>
/// <param name="SubtitlePath">Path of the subtitle.</param>
public sealed record SubtitleVideoPair(string VideoPath, string SubtitlePath);

/// <summary>
/// Result of pairing subtitle files with videos.
/// </summary>
/// <param name="Pairs">Subtitles that could be matched to a video.</param>
/// <param name="Unmatched">Candidate subtitles with no matching video in their folder.</param>
public sealed record SubtitleScanResult(IReadOnlyList<SubtitleVideoPair> Pairs, IReadOnlyList<string> Unmatched);

/// <summary>
/// Finds subtitle files on disk and matches them to their videos using
/// Jellyfin's naming convention: a subtitle belongs to the video in the same
/// folder whose file name (without extension) it starts with, e.g.
/// "Movie.en.forced.srt" belongs to "Movie.mkv".
/// </summary>
public static class SubtitleFileScanner
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3gp", ".avi", ".flv", ".m2ts", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg",
        ".mpg", ".mts", ".ogm", ".ogv", ".ts", ".vob", ".webm", ".wmv",
    };

    /// <summary>
    /// Lists all files below the given folders, recursively. Missing folders
    /// and folders that can't be read are skipped.
    /// </summary>
    /// <param name="rootFolders">Library folders to search.</param>
    /// <returns>File paths.</returns>
    public static IEnumerable<string> EnumerateFiles(IEnumerable<string> rootFolders)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

        return rootFolders
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*", options));
    }

    /// <summary>
    /// Matches every candidate subtitle in <paramref name="files"/> to a video
    /// in the same folder. Already-aligned outputs are not candidates.
    /// </summary>
    /// <param name="files">File paths, e.g. from <see cref="EnumerateFiles"/>.</param>
    /// <returns>The matched pairs, and the candidate subtitles without a video.</returns>
    public static SubtitleScanResult Pair(IEnumerable<string> files)
    {
        var pairs = new List<SubtitleVideoPair>();
        var unmatched = new List<string>();

        foreach (var folder in files.GroupBy(f => Path.GetDirectoryName(f) ?? string.Empty, StringComparer.Ordinal))
        {
            var videos = folder
                .Where(f => VideoExtensions.Contains(Path.GetExtension(f)))
                .Select(f => (Path: f, Name: Path.GetFileNameWithoutExtension(f)))
                .ToList();

            foreach (var subtitle in folder.Where(SubtitleChangeDetector.IsCandidateSubtitle))
            {
                var subtitleName = Path.GetFileNameWithoutExtension(subtitle);

                // Longest name wins, so "Movie.Extended.en.srt" goes to
                // "Movie.Extended.mkv" rather than "Movie.mkv".
                var video = videos
                    .Where(v => subtitleName.Equals(v.Name, StringComparison.OrdinalIgnoreCase)
                        || subtitleName.StartsWith(v.Name + ".", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(v => v.Name.Length)
                    .Select(v => v.Path)
                    .FirstOrDefault();

                if (video is null)
                {
                    unmatched.Add(subtitle);
                }
                else
                {
                    pairs.Add(new SubtitleVideoPair(video, subtitle));
                }
            }
        }

        return new SubtitleScanResult(pairs, unmatched);
    }
}
