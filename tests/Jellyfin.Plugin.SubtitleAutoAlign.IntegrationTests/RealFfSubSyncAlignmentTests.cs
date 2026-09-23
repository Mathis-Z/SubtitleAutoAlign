using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Configuration;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.IntegrationTests;

/// <summary>
/// Exercises <see cref="SubtitleAlignmentService"/> against a real ffsubsync
/// binary and real fixture media. Only meaningful when run inside the
/// docker/integration test image, which has ffsubsync and ffmpeg installed.
/// Excluded from the normal fast unit-test run via the "Integration" trait.
/// </summary>
[Trait("Category", "Integration")]
public class RealFfSubSyncAlignmentTests : IDisposable
{
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    private static readonly string VideoFixturePath = Path.Combine(FixturesDirectory, "sample.mp4");
    private static readonly string SubtitleFixturePath = Path.Combine(FixturesDirectory, "sample.en.srt");

    private readonly string _workDirectory;

    public RealFfSubSyncAlignmentTests()
    {
        _workDirectory = Directory.CreateTempSubdirectory("subtitle-auto-align-it-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_workDirectory, recursive: true);
    }

    [Fact]
    public async Task AlignAsync_RealFfSubSync_ProducesValidAlignedSubtitle()
    {
        Assert.True(
            File.Exists(VideoFixturePath) && File.Exists(SubtitleFixturePath),
            $"Expected fixture files at {VideoFixturePath} and {SubtitleFixturePath}. " +
            "Add sample.mp4 and sample.en.srt to the Fixtures directory before running integration tests.");

        var videoPath = Path.Combine(_workDirectory, "sample.mp4");
        var subtitlePath = Path.Combine(_workDirectory, "sample.en.srt");
        File.Copy(VideoFixturePath, videoPath);
        File.Copy(SubtitleFixturePath, subtitlePath);

        var configuration = new PluginConfiguration
        {
            EnableAutoAlign = true,
            FfSubSyncPath = "ffsubsync",
            TimeoutSeconds = 120,
        };

        var service = new SubtitleAlignmentService(
            new ProcessRunner(),
            new FfSubSyncLocator(_workDirectory),
            () => configuration,
            NullLogger<SubtitleAlignmentService>.Instance);

        var request = new AlignmentRequest(Guid.NewGuid(), videoPath, subtitlePath);
        var result = await service.AlignAsync(request, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(result.OutputPath));

        var outputContent = await File.ReadAllTextAsync(result.OutputPath);
        Assert.False(string.IsNullOrWhiteSpace(outputContent));
        Assert.Contains("-->", outputContent);
    }
}
