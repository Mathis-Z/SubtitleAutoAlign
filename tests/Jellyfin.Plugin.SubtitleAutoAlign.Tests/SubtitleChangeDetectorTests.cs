using System.Collections.Generic;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class SubtitleChangeDetectorTests
{
    [Fact]
    public void DiffNewSubtitlePaths_ReturnsOnlyNewPaths()
    {
        var previous = new List<string> { "a.srt", "b.srt" };
        var current = new List<string> { "a.srt", "b.srt", "c.srt" };

        var result = SubtitleChangeDetector.DiffNewSubtitlePaths(previous, current);

        Assert.Equal(new[] { "c.srt" }, result);
    }

    [Fact]
    public void DiffNewSubtitlePaths_NoNewPaths_ReturnsEmpty()
    {
        var previous = new List<string> { "a.srt" };
        var current = new List<string> { "a.srt" };

        var result = SubtitleChangeDetector.DiffNewSubtitlePaths(previous, current);

        Assert.Empty(result);
    }

    [Fact]
    public void DiffNewSubtitlePaths_EmptyPrevious_ReturnsAllCurrent()
    {
        var previous = new List<string>();
        var current = new List<string> { "a.srt", "b.srt" };

        var result = SubtitleChangeDetector.DiffNewSubtitlePaths(previous, current);

        Assert.Equal(current, result);
    }

    [Theory]
    [InlineData("Movie.en.srt", true)]
    [InlineData("Movie.ass", true)]
    [InlineData("Movie.vtt", true)]
    [InlineData("Movie.en.autoaligned.srt", false)]
    [InlineData("Movie.txt", false)]
    [InlineData("", false)]
    public void IsCandidateSubtitle_FiltersCorrectly(string path, bool expected)
    {
        Assert.Equal(expected, SubtitleChangeDetector.IsCandidateSubtitle(path));
    }
}
