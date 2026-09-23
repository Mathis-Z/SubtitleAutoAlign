using System;
using System.IO;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// Pure helpers for deriving the aligned-output subtitle path and recognizing
/// files the plugin has already produced, so they are never re-processed.
/// </summary>
public static class SubtitleFileNaming
{
    /// <summary>
    /// The marker inserted into a subtitle's filename to indicate it is the
    /// plugin's aligned output, e.g. "Movie.en.srt" -&gt; "Movie.en.autoaligned.srt".
    /// </summary>
    public const string AlignedSuffix = "autoaligned";

    /// <summary>
    /// Builds the output path for the aligned copy of a subtitle file.
    /// </summary>
    /// <param name="originalSubtitlePath">Path to the original, unaligned subtitle file.</param>
    /// <returns>Path with the aligned suffix inserted before the file extension.</returns>
    public static string BuildAlignedOutputPath(string originalSubtitlePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalSubtitlePath);

        var directory = Path.GetDirectoryName(originalSubtitlePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalSubtitlePath);
        var extension = Path.GetExtension(originalSubtitlePath);

        var alignedFileName = $"{fileNameWithoutExtension}.{AlignedSuffix}{extension}";

        return string.IsNullOrEmpty(directory)
            ? alignedFileName
            : Path.Combine(directory, alignedFileName);
    }

    /// <summary>
    /// Determines whether the given subtitle path is already an aligned output
    /// produced by this plugin (and therefore must not be re-processed).
    /// </summary>
    /// <param name="subtitlePath">Path to check.</param>
    /// <returns>True if the filename contains the aligned marker.</returns>
    public static bool IsAlreadyAligned(string subtitlePath)
    {
        if (string.IsNullOrEmpty(subtitlePath))
        {
            return false;
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(subtitlePath);
        return fileNameWithoutExtension.EndsWith(
            "." + AlignedSuffix,
            StringComparison.OrdinalIgnoreCase);
    }
}
