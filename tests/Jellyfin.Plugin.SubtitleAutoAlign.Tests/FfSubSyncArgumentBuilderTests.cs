using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class FfSubSyncArgumentBuilderTests
{
    [Fact]
    public void Build_NoExtraArguments_ProducesCoreArguments()
    {
        var result = FfSubSyncArgumentBuilder.Build("video.mkv", "sub.srt", "out.srt");

        Assert.Equal(new[] { "video.mkv", "-i", "sub.srt", "-o", "out.srt" }, result);
    }

    [Fact]
    public void Build_WithExtraArguments_AppendsTokenizedArgs()
    {
        var result = FfSubSyncArgumentBuilder.Build(
            "video.mkv",
            "sub.srt",
            "out.srt",
            "--max-offset-seconds 60 --gss");

        Assert.Equal(
            new[] { "video.mkv", "-i", "sub.srt", "-o", "out.srt", "--max-offset-seconds", "60", "--gss" },
            result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TokenizeExtraArguments_EmptyOrNull_ReturnsEmpty(string? input)
    {
        var result = FfSubSyncArgumentBuilder.TokenizeExtraArguments(input);

        Assert.Empty(result);
    }

    [Fact]
    public void TokenizeExtraArguments_HonorsQuotedSegmentsAsSingleToken()
    {
        var result = FfSubSyncArgumentBuilder.TokenizeExtraArguments("--reference \"my video.mkv\" --gss");

        Assert.Equal(new[] { "--reference", "my video.mkv", "--gss" }, result);
    }

    [Fact]
    public void TokenizeExtraArguments_CollapsesMultipleSpaces()
    {
        var result = FfSubSyncArgumentBuilder.TokenizeExtraArguments("--gss    --vad=auditok");

        Assert.Equal(new[] { "--gss", "--vad=auditok" }, result);
    }
}
