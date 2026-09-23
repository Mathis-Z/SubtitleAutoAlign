using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Configuration;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class SubtitleAlignmentServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public SubtitleAlignmentServiceTests()
    {
        _tempDirectory = Directory.CreateTempSubdirectory("subtitle-auto-align-tests-").FullName;
    }

    public void Dispose()
    {
        Directory.Delete(_tempDirectory, recursive: true);
    }

    [Fact]
    public async Task AlignAsync_Success_ReturnsSuccessResultWithOutputPath()
    {
        var (service, runner) = CreateService(new PluginConfiguration());
        runner.ResultToReturn = new ProcessRunResult(0, string.Empty, string.Empty, false);

        var request = new AlignmentRequest(Guid.NewGuid(), VideoPath(), SubtitlePath());
        var result = await service.AlignAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.Skipped);
        Assert.Equal(SubtitleFileNaming.BuildAlignedOutputPath(SubtitlePath()), result.OutputPath);
        Assert.Equal(1, runner.CallCount);
    }

    [Fact]
    public async Task AlignAsync_NonZeroExitCode_ReturnsFailureWithoutThrowing()
    {
        var (service, runner) = CreateService(new PluginConfiguration());
        runner.ResultToReturn = new ProcessRunResult(1, string.Empty, "boom", false);

        var request = new AlignmentRequest(Guid.NewGuid(), VideoPath(), SubtitlePath());
        var result = await service.AlignAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Skipped);
        Assert.Contains("boom", result.ErrorMessage);
    }

    [Fact]
    public async Task AlignAsync_TimedOut_ReturnsFailureWithoutThrowing()
    {
        var (service, runner) = CreateService(new PluginConfiguration());
        runner.ResultToReturn = new ProcessRunResult(-1, string.Empty, string.Empty, true);

        var request = new AlignmentRequest(Guid.NewGuid(), VideoPath(), SubtitlePath());
        var result = await service.AlignAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Skipped);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task AlignAsync_Disabled_SkipsWithoutInvokingRunner()
    {
        var (service, runner) = CreateService(new PluginConfiguration { EnableAutoAlign = false });

        var request = new AlignmentRequest(Guid.NewGuid(), VideoPath(), SubtitlePath());
        var result = await service.AlignAsync(request, CancellationToken.None);

        Assert.True(result.Skipped);
        Assert.False(result.Success);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task AlignAsync_OutputAlreadyExists_SkipsUnlessOverwriteConfigured()
    {
        var (service, runner) = CreateService(new PluginConfiguration { OverwriteExistingAlignedSubtitle = false });
        var outputPath = SubtitleFileNaming.BuildAlignedOutputPath(SubtitlePath());
        File.WriteAllText(outputPath, "existing");

        var request = new AlignmentRequest(Guid.NewGuid(), VideoPath(), SubtitlePath());
        var result = await service.AlignAsync(request, CancellationToken.None);

        Assert.True(result.Skipped);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task AlignAsync_OutputAlreadyExists_OverwriteConfigured_RunsAnyway()
    {
        var (service, runner) = CreateService(new PluginConfiguration { OverwriteExistingAlignedSubtitle = true });
        var outputPath = SubtitleFileNaming.BuildAlignedOutputPath(SubtitlePath());
        File.WriteAllText(outputPath, "existing");
        runner.ResultToReturn = new ProcessRunResult(0, string.Empty, string.Empty, false);

        var request = new AlignmentRequest(Guid.NewGuid(), VideoPath(), SubtitlePath());
        var result = await service.AlignAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, runner.CallCount);
    }

    [Fact]
    public async Task AlignAsync_FfmpegDirectoryKnown_PassesFfmpegPathToFfSubSync()
    {
        var (service, runner) = CreateService(new PluginConfiguration(), ffmpegDirectory: "/usr/lib/jellyfin-ffmpeg");

        var request = new AlignmentRequest(Guid.NewGuid(), VideoPath(), SubtitlePath());
        await service.AlignAsync(request, CancellationToken.None);

        var arguments = Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<string>>(runner.LastArguments);
        var flagIndex = Assert.Single(Enumerable.Range(0, arguments.Count), i => arguments[i] == "--ffmpeg-path");
        Assert.Equal("/usr/lib/jellyfin-ffmpeg", arguments[flagIndex + 1]);
    }

    [Fact]
    public async Task AlignAsync_FfmpegDirectoryUnknown_OmitsFfmpegPath()
    {
        var (service, runner) = CreateService(new PluginConfiguration(), ffmpegDirectory: null);

        var request = new AlignmentRequest(Guid.NewGuid(), VideoPath(), SubtitlePath());
        await service.AlignAsync(request, CancellationToken.None);

        Assert.DoesNotContain("--ffmpeg-path", runner.LastArguments!);
    }

    private (SubtitleAlignmentService Service, FakeProcessRunner Runner) CreateService(
        PluginConfiguration configuration,
        string? ffmpegDirectory = null)
    {
        var runner = new FakeProcessRunner();
        var service = new SubtitleAlignmentService(
            runner,
            new FfSubSyncLocator(_tempDirectory),
            () => configuration,
            () => ffmpegDirectory,
            NullLogger<SubtitleAlignmentService>.Instance);
        return (service, runner);
    }

    private string VideoPath() => Path.Combine(_tempDirectory, "Movie.mkv");

    private string SubtitlePath() => Path.Combine(_tempDirectory, "Movie.en.srt");
}
