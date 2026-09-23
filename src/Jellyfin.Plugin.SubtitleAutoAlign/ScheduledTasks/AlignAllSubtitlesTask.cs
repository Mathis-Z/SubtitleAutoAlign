using System;
using System.Collections.Generic;
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
/// "Align all subtitles": aligns every external subtitle of every movie in
/// the library that has no aligned copy yet. Runs from Dashboard &gt; Scheduled
/// Tasks or the plugin's settings page; has no default schedule.
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
        "Runs ffsubsync on every external subtitle of every movie that doesn't have an aligned copy yet.";

    /// <inheritdoc />
    public string Category => "Subtitle Auto Align";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var movies = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            IsVirtualItem = false,
            Recursive = true,
        }).OfType<Movie>();

        var requests = movies
            .Where(movie => !string.IsNullOrEmpty(movie.Path))
            .SelectMany(movie => SubtitleDiscovery.GetExternalSubtitlePaths(movie)
                .Where(SubtitleChangeDetector.IsCandidateSubtitle)
                .Select(subtitle => new AlignmentRequest(movie.Id, movie.Path, subtitle)))
            .ToList();

        _logger.LogInformation("Aligning {Count} subtitle file(s) across the library", requests.Count);

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
