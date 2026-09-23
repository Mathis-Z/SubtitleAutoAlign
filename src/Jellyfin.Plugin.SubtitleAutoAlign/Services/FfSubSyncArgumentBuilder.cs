using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// Builds the ffsubsync command-line argument list. Kept pure and free of any
/// process-execution concerns so argument shape can be unit tested directly.
/// </summary>
public static class FfSubSyncArgumentBuilder
{
    /// <summary>
    /// Builds the argument list for an ffsubsync invocation:
    /// <c>ffsubsync &lt;video&gt; -i &lt;subtitle&gt; -o &lt;output&gt; [extra args]</c>.
    /// </summary>
    /// <param name="videoPath">Path to the reference video file.</param>
    /// <param name="subtitlePath">Path to the subtitle to align.</param>
    /// <param name="outputPath">Path to write the aligned subtitle to.</param>
    /// <param name="extraArguments">Optional extra CLI arguments, space-separated and
    /// possibly containing double-quoted segments (e.g. "--max-offset-seconds 60").</param>
    /// <returns>Ordered argument list, suitable for <c>ProcessStartInfo.ArgumentList</c>.</returns>
    public static IReadOnlyList<string> Build(
        string videoPath,
        string subtitlePath,
        string outputPath,
        string? extraArguments = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);
        ArgumentException.ThrowIfNullOrEmpty(subtitlePath);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);

        var arguments = new List<string>
        {
            videoPath,
            "-i",
            subtitlePath,
            "-o",
            outputPath,
        };

        arguments.AddRange(TokenizeExtraArguments(extraArguments));

        return arguments;
    }

    /// <summary>
    /// Splits a user-supplied extra-arguments string into individual tokens,
    /// honoring double-quoted segments as single tokens (e.g. so a value with
    /// spaces can be passed as one argument).
    /// </summary>
    /// <param name="extraArguments">Raw extra-arguments string, or null/empty.</param>
    /// <returns>Ordered list of tokens; empty if input is null, empty, or whitespace.</returns>
    public static IReadOnlyList<string> TokenizeExtraArguments(string? extraArguments)
    {
        var tokens = new List<string>();

        if (string.IsNullOrWhiteSpace(extraArguments))
        {
            return tokens;
        }

        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var c in extraArguments)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
