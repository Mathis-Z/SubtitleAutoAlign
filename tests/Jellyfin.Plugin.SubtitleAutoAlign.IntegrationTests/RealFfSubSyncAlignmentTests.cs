using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Configuration;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests;

/// <summary>
/// Runs <see cref="SubtitleAlignmentService"/> against a real ffsubsync binary
/// using the first five minutes of "Us Now" (2009) and its English subtitle,
/// which is in sync with the film. Each test shifts the subtitle by a known
/// amount and checks that alignment restores the original timings.
/// Uses "ffsubsync" from PATH, or the binary named by FFSUBSYNC_PATH.
/// </summary>
[Trait("Category", "Integration")]
public class RealFfSubSyncAlignmentTests : IDisposable
{
    // The fixture subtitle itself measures about -0.45s against the clip,
    // so allow for that plus ffsubsync's own frame-level precision.
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(1);

    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    private static readonly string VideoFixturePath = Path.Combine(FixturesDirectory, "us-now-5min.mp4");
    private static readonly string SubtitleFixturePath = Path.Combine(FixturesDirectory, "us-now-5min.en.srt");

    private readonly string _workDirectory;

    public RealFfSubSyncAlignmentTests()
    {
        _workDirectory = Directory.CreateTempSubdirectory("subtitle-auto-align-it-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_workDirectory, recursive: true);
    }

    [Theory]
    [InlineData(6.0)]
    [InlineData(-2.5)] // The first cue starts at 0:03; a larger negative shift would clamp it at zero.
    [InlineData(25.0)]
    public async Task AlignAsync_ShiftedSubtitle_RestoresOriginalTimings(double shiftSeconds)
    {
        var original = await File.ReadAllTextAsync(SubtitleFixturePath);
        var shifted = SrtTimings.Shift(original, TimeSpan.FromSeconds(shiftSeconds));

        var result = await AlignAsync(shifted);

        Assert.True(result.Success, result.ErrorMessage);
        AssertTimingsMatch(SrtTimings.CueStarts(original), SrtTimings.CueStarts(await File.ReadAllTextAsync(result.OutputPath)));
    }

    [Fact]
    public async Task AlignAsync_AlreadySyncedSubtitle_KeepsTimings()
    {
        var original = await File.ReadAllTextAsync(SubtitleFixturePath);

        var result = await AlignAsync(original);

        Assert.True(result.Success, result.ErrorMessage);
        AssertTimingsMatch(SrtTimings.CueStarts(original), SrtTimings.CueStarts(await File.ReadAllTextAsync(result.OutputPath)));
    }

    [Fact]
    public async Task AlignAsync_WritesAutoalignedFileNextToOriginal()
    {
        var result = await AlignAsync(await File.ReadAllTextAsync(SubtitleFixturePath));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(Path.Combine(_workDirectory, "us-now-5min.en.autoaligned.srt"), result.OutputPath);
        Assert.True(File.Exists(Path.Combine(_workDirectory, "us-now-5min.en.srt")), "Original subtitle must be left in place.");
    }

    private async Task<AlignmentResult> AlignAsync(string subtitleContent)
    {
        var videoPath = Path.Combine(_workDirectory, "us-now-5min.mp4");
        var subtitlePath = Path.Combine(_workDirectory, "us-now-5min.en.srt");
        File.Copy(VideoFixturePath, videoPath, overwrite: true);
        await File.WriteAllTextAsync(subtitlePath, subtitleContent);

        var configuration = new PluginConfiguration
        {
            EnableAutoAlign = true,
            FfSubSyncPath = Environment.GetEnvironmentVariable("FFSUBSYNC_PATH") is { Length: > 0 } path ? path : "ffsubsync",
            TimeoutSeconds = 120,
        };

        var service = new SubtitleAlignmentService(
            new ProcessRunner(),
            new FfSubSyncLocator(_workDirectory),
            () => configuration,
            () => null,
            NullLogger<SubtitleAlignmentService>.Instance);

        return await service.AlignAsync(new AlignmentRequest(Guid.NewGuid(), videoPath, subtitlePath), CancellationToken.None);
    }

    private static void AssertTimingsMatch(System.Collections.Generic.IReadOnlyList<TimeSpan> expected, System.Collections.Generic.IReadOnlyList<TimeSpan> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        var worst = expected.Zip(actual, (e, a) => (a - e).Duration()).Max();
        Assert.True(worst <= Tolerance, $"Aligned cue starts differ from the originals by up to {worst.TotalSeconds:F3}s (tolerance {Tolerance.TotalSeconds}s).");
    }
}
