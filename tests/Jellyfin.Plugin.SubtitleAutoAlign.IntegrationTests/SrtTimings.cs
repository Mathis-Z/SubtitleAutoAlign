using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests;

/// <summary>
/// Minimal SRT timestamp helpers for building deliberately misaligned
/// subtitles and checking how well alignment restored them.
/// </summary>
public static partial class SrtTimings
{
    [GeneratedRegex(@"(\d+):(\d+):(\d+),(\d+) --> (\d+):(\d+):(\d+),(\d+)")]
    private static partial Regex CueTimingLine();

    [GeneratedRegex(@"(\d+):(\d+):(\d+),(\d+)")]
    private static partial Regex Timestamp();

    public static IReadOnlyList<TimeSpan> CueStarts(string srtContent)
    {
        return CueTimingLine().Matches(srtContent)
            .Select(m => ToTimeSpan(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value))
            .ToList();
    }

    public static string Shift(string srtContent, TimeSpan offset)
    {
        return Timestamp().Replace(srtContent, m =>
        {
            var shifted = ToTimeSpan(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value) + offset;
            if (shifted < TimeSpan.Zero)
            {
                shifted = TimeSpan.Zero;
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}:{2:00},{3:000}",
                (int)shifted.TotalHours,
                shifted.Minutes,
                shifted.Seconds,
                shifted.Milliseconds);
        });
    }

    private static TimeSpan ToTimeSpan(string hours, string minutes, string seconds, string milliseconds)
    {
        return new TimeSpan(
            0,
            int.Parse(hours, CultureInfo.InvariantCulture),
            int.Parse(minutes, CultureInfo.InvariantCulture),
            int.Parse(seconds, CultureInfo.InvariantCulture),
            int.Parse(milliseconds, CultureInfo.InvariantCulture));
    }
}
