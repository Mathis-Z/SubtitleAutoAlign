using System.IO;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class SubtitleFileNamingTests
{
    [Theory]
    [InlineData("Movie.en.srt", "Movie.en.autoaligned.srt")]
    [InlineData("Movie.srt", "Movie.autoaligned.srt")]
    [InlineData("Movie.en.forced.srt", "Movie.en.forced.autoaligned.srt")]
    [InlineData("Movie", "Movie.autoaligned")]
    public void BuildAlignedOutputPath_InsertsSuffixBeforeExtension(string input, string expectedFileName)
    {
        var result = SubtitleFileNaming.BuildAlignedOutputPath(input);

        Assert.Equal(expectedFileName, result);
    }

    [Fact]
    public void BuildAlignedOutputPath_PreservesDirectory()
    {
        var input = Path.Combine("movies", "Some Movie", "Some Movie.en.srt");

        var result = SubtitleFileNaming.BuildAlignedOutputPath(input);

        Assert.Equal(
            Path.Combine("movies", "Some Movie", "Some Movie.en.autoaligned.srt"),
            result);
    }

    [Theory]
    [InlineData("Movie.en.autoaligned.srt", true)]
    [InlineData("Movie.autoaligned.srt", true)]
    [InlineData("Movie.AUTOALIGNED.srt", true)]
    [InlineData("Movie.en.srt", false)]
    [InlineData("Movie.srt", false)]
    public void IsAlreadyAligned_DetectsMarker(string input, bool expected)
    {
        Assert.Equal(expected, SubtitleFileNaming.IsAlreadyAligned(input));
    }

    [Fact]
    public void IsAlreadyAligned_EmptyPath_ReturnsFalse()
    {
        Assert.False(SubtitleFileNaming.IsAlreadyAligned(string.Empty));
    }
}
