using System;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class SubtitleFileScannerTests
{
    private static readonly string Movies = Path.Combine("media", "Movies");

    [Fact]
    public void Pair_MatchesSubtitleToVideoWithSameName()
    {
        var result = SubtitleFileScanner.Pair([P("Drive", "Drive.mp4"), P("Drive", "Drive.en.srt")]);

        Assert.Equal([new SubtitleVideoPair(P("Drive", "Drive.mp4"), P("Drive", "Drive.en.srt"))], result.Pairs);
        Assert.Empty(result.Unmatched);
    }

    [Theory]
    [InlineData("Drive.srt")]
    [InlineData("Drive.en.srt")]
    [InlineData("Drive.en.forced.srt")]
    [InlineData("drive.EN.ass")]
    public void Pair_AcceptsJellyfinSubtitleNames(string subtitle)
    {
        var result = SubtitleFileScanner.Pair([P("Drive", "Drive.mkv"), P("Drive", subtitle)]);

        Assert.Single(result.Pairs);
    }

    [Fact]
    public void Pair_PrefersTheLongestMatchingVideoName()
    {
        var result = SubtitleFileScanner.Pair(
        [
            P("Drive", "Drive.mkv"),
            P("Drive", "Drive.Extended.mkv"),
            P("Drive", "Drive.Extended.en.srt"),
        ]);

        Assert.Equal(P("Drive", "Drive.Extended.mkv"), Assert.Single(result.Pairs).VideoPath);
    }

    [Fact]
    public void Pair_DoesNotMatchVideoWhoseNameIsOnlyAPrefixOfAWord()
    {
        // "Drive2.en.srt" must not be treated as a subtitle for "Drive.mkv".
        var result = SubtitleFileScanner.Pair([P("Drive", "Drive.mkv"), P("Drive", "Drive2.en.srt")]);

        Assert.Empty(result.Pairs);
        Assert.Equal([P("Drive", "Drive2.en.srt")], result.Unmatched);
    }

    [Fact]
    public void Pair_OnlyMatchesVideosInTheSameFolder()
    {
        var result = SubtitleFileScanner.Pair([P("Drive", "Drive.mkv"), P("Other", "Drive.en.srt")]);

        Assert.Empty(result.Pairs);
        Assert.Single(result.Unmatched);
    }

    [Fact]
    public void Pair_SkipsAlignedOutputsAndNonSubtitles()
    {
        var result = SubtitleFileScanner.Pair(
        [
            P("Drive", "Drive.mkv"),
            P("Drive", "Drive.en.autoaligned.srt"),
            P("Drive", "Drive.nfo"),
            P("Drive", "poster.jpg"),
        ]);

        Assert.Empty(result.Pairs);
        Assert.Empty(result.Unmatched);
    }

    [Fact]
    public void Pair_HandlesMultipleSubtitlesAndEpisodes()
    {
        var season = Path.Combine("media", "Shows", "Show", "Season 1");
        var result = SubtitleFileScanner.Pair(
        [
            Path.Combine(season, "Show S01E01.mkv"),
            Path.Combine(season, "Show S01E01.en.srt"),
            Path.Combine(season, "Show S01E01.de.srt"),
            Path.Combine(season, "Show S01E02.mkv"),
            Path.Combine(season, "Show S01E02.en.srt"),
        ]);

        Assert.Equal(3, result.Pairs.Count);
        Assert.Equal(
            [Path.Combine(season, "Show S01E01.mkv"), Path.Combine(season, "Show S01E01.mkv"), Path.Combine(season, "Show S01E02.mkv")],
            result.Pairs.Select(p => p.VideoPath).OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void EnumerateFiles_SearchesRecursivelyAndSkipsMissingFolders()
    {
        var root = Directory.CreateTempSubdirectory("subtitle-scanner-tests-").FullName;
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root, "Shows", "Show", "Season 1")).FullName;
            File.WriteAllText(Path.Combine(root, "Movie.mkv"), string.Empty);
            File.WriteAllText(Path.Combine(nested, "Show S01E01.mkv"), string.Empty);
            File.WriteAllText(Path.Combine(nested, "Show S01E01.en.srt"), string.Empty);

            var files = SubtitleFileScanner.EnumerateFiles([root, Path.Combine(root, "does-not-exist")]).ToList();

            Assert.Equal(3, files.Count);
            Assert.Contains(Path.Combine(nested, "Show S01E01.en.srt"), files);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string P(string folder, string file) => Path.Combine(Movies, folder, file);
}
