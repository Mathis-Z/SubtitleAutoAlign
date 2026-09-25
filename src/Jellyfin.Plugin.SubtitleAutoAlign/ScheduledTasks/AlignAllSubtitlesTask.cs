using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleAutoAlign.ScheduledTasks;

/// <summary>
/// "Align all subtitles": searches the library folders recursively for
/// subtitle files next to their videos, adds the subtitles Jellyfin has
/// indexed for movies, and aligns each one that has no aligned copy yet.
/// Runs from Dashboard &gt; Scheduled Tasks or the plugin's settings page; has
/// no default schedule.
/// </summary>
public sealed class AlignAllSubtitlesTask : IScheduledTask
{
    /// <summary>
    /// The task key, used by the settings page to find and start this task.
    /// </summary>
    public const string TaskKey = "SubtitleAutoAlignAll";

    private readonly ILibraryManager _libraryManager;
    private readonly ISubtitleAlignmentService _alignmentService;
    private readonly ILogger<AlignAllSubtitlesTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlignAllSubtitlesTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager, used to find the movies.</param>
    /// <param name="alignmentService">Service that performs each alignment.</param>
    /// <param name="logger">Logger.</param>
    public AlignAllSubtitlesTask(
        ILibraryManager libraryManager,
        ISubtitleAlignmentService alignmentService,
        ILogger<AlignAllSubtitlesTask> logger)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _alignmentService = alignmentService ?? throw new ArgumentNullException(nameof(alignmentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Align all subtitles";

    /// <inheritdoc />
    public string Key => TaskKey;

    /// <inheritdoc />
    public string Description =>
        "Searches all library folders for subtitle files next to their videos (plus subtitles Jellyfin has indexed for movies) and aligns every one that doesn't have an aligned copy yet.";

    /// <inheritdoc />
    public string Category => "Subtitle Auto Align";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        // Subtitles on disk next to their videos, anywhere below the library
        // folders, including ones Jellyfin hasn't picked up yet.
        var libraryFolders = _libraryManager.GetVirtualFolders().SelectMany(folder => folder.Locations).Distinct().ToList();
        var scan = SubtitleFileScanner.Pair(SubtitleFileScanner.EnumerateFiles(libraryFolders));
        var fromDisk = scan.Pairs.Select(pair => new AlignmentRequest(Guid.Empty, pair.VideoPath, pair.SubtitlePath));

        // Subtitles Jellyfin has indexed for movies. These can live outside the
        // library folders when "save subtitles with media" is off.
        var fromLibrary = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = [BaseItemKind.Movie],
                IsVirtualItem = false,
                Recursive = true,
            })
            .OfType<Movie>()
            .Where(movie => !string.IsNullOrEmpty(movie.Path))
            .SelectMany(movie => SubtitleDiscovery.GetExternalSubtitlePaths(movie)
                .Where(SubtitleChangeDetector.IsCandidateSubtitle)
                .Select(subtitle => new AlignmentRequest(movie.Id, movie.Path, subtitle)));

        var overwrite = Plugin.Instance?.Configuration.OverwriteExistingAlignedSubtitle ?? false;
        var requests = fromLibrary.Concat(fromDisk)
            .DistinctBy(request => request.SubtitlePath, StringComparer.Ordinal)
            .Where(request => overwrite || !File.Exists(SubtitleFileNaming.BuildAlignedOutputPath(request.SubtitlePath)))
            .ToList();

        _logger.LogInformation(
            "Found {Count} subtitle file(s) to align in {FolderCount} library folder(s)",
            requests.Count,
            libraryFolders.Count);
        if (scan.Unmatched.Count > 0)
        {
            _logger.LogInformation(
                "Ignoring {Count} subtitle file(s) with no matching video next to them: {Paths}",
                scan.Unmatched.Count,
                string.Join(", ", scan.Unmatched));
        }

        var summary = await BulkAligner.RunAsync(requests, _alignmentService, progress, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Finished aligning all subtitles: {Aligned} aligned, {Skipped} skipped, {Failed} failed",
            summary.Aligned,
            summary.Skipped,
            summary.Failed);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];
}
