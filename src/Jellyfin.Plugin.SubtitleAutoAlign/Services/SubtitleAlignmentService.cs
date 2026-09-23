using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Configuration;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <inheritdoc />
public sealed class SubtitleAlignmentService : ISubtitleAlignmentService
{
    private readonly IProcessRunner _processRunner;
    private readonly IFfSubSyncLocator _ffSubSyncLocator;
    private readonly Func<PluginConfiguration> _configurationAccessor;
    private readonly Func<string?> _ffmpegDirectoryAccessor;
    private readonly ILogger<SubtitleAlignmentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleAlignmentService"/> class.
    /// </summary>
    /// <param name="processRunner">Abstraction used to invoke ffsubsync.</param>
    /// <param name="ffSubSyncLocator">Resolves which ffsubsync executable to invoke.</param>
    /// <param name="configurationAccessor">Delegate returning the current plugin configuration.</param>
    /// <param name="ffmpegDirectoryAccessor">Delegate returning the directory holding the ffmpeg/ffprobe
    /// binaries to use (Jellyfin's own), or null to let ffsubsync search PATH.</param>
    /// <param name="logger">Logger.</param>
    public SubtitleAlignmentService(
        IProcessRunner processRunner,
        IFfSubSyncLocator ffSubSyncLocator,
        Func<PluginConfiguration> configurationAccessor,
        Func<string?> ffmpegDirectoryAccessor,
        ILogger<SubtitleAlignmentService> logger)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _ffSubSyncLocator = ffSubSyncLocator ?? throw new ArgumentNullException(nameof(ffSubSyncLocator));
        _configurationAccessor = configurationAccessor ?? throw new ArgumentNullException(nameof(configurationAccessor));
        _ffmpegDirectoryAccessor = ffmpegDirectoryAccessor ?? throw new ArgumentNullException(nameof(ffmpegDirectoryAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AlignmentResult> AlignAsync(AlignmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuration = _configurationAccessor();
        var outputPath = SubtitleFileNaming.BuildAlignedOutputPath(request.SubtitlePath);

        if (!configuration.EnableAutoAlign)
        {
            _logger.LogDebug("Auto-align disabled; skipping {SubtitlePath}", request.SubtitlePath);
            return new AlignmentResult(Success: false, OutputPath: outputPath, Skipped: true, ErrorMessage: null);
        }

        if (File.Exists(outputPath) && !configuration.OverwriteExistingAlignedSubtitle)
        {
            _logger.LogDebug("Aligned subtitle already exists at {OutputPath}; skipping", outputPath);
            return new AlignmentResult(Success: false, OutputPath: outputPath, Skipped: true, ErrorMessage: null);
        }

        var arguments = FfSubSyncArgumentBuilder.Build(
            request.VideoPath,
            request.SubtitlePath,
            outputPath,
            configuration.ExtraArguments,
            _ffmpegDirectoryAccessor());

        var ffSubSyncPath = _ffSubSyncLocator.Resolve(configuration.FfSubSyncPath);

        _logger.LogInformation(
            "Aligning subtitle {SubtitlePath} against {VideoPath} -> {OutputPath} (using {FfSubSyncPath})",
            request.SubtitlePath,
            request.VideoPath,
            outputPath,
            ffSubSyncPath);

        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync(
                ffSubSyncPath,
                arguments,
                TimeSpan.FromSeconds(configuration.TimeoutSeconds),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to run ffsubsync for {SubtitlePath}", request.SubtitlePath);
            return new AlignmentResult(Success: false, OutputPath: outputPath, Skipped: false, ErrorMessage: ex.Message);
        }

        if (result.TimedOut)
        {
            _logger.LogWarning("ffsubsync timed out aligning {SubtitlePath}", request.SubtitlePath);
            return new AlignmentResult(Success: false, OutputPath: outputPath, Skipped: false, ErrorMessage: "ffsubsync timed out.");
        }

        if (result.ExitCode != 0)
        {
            _logger.LogWarning(
                "ffsubsync exited with code {ExitCode} for {SubtitlePath}: {StandardError}",
                result.ExitCode,
                request.SubtitlePath,
                result.StandardError);
            return new AlignmentResult(
                Success: false,
                OutputPath: outputPath,
                Skipped: false,
                ErrorMessage: $"ffsubsync exited with code {result.ExitCode}: {result.StandardError}");
        }

        _logger.LogInformation("Successfully aligned subtitle to {OutputPath}", outputPath);
        return new AlignmentResult(Success: true, OutputPath: outputPath, Skipped: false, ErrorMessage: null);
    }
}
