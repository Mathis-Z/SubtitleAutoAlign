using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleAutoAlign.EventSubscribers;

/// <summary>
/// Watches the Jellyfin library for newly-appeared external subtitle files on
/// movies and enqueues them for auto-alignment.
///
/// Jellyfin does not expose a confirmed, ABI-stable "subtitle downloaded"
/// event, so this watches <see cref="ILibraryManager.ItemUpdated"/> and diffs
/// each movie's external subtitle files against a previously observed
/// snapshot. It does not attempt to prove a subtitle came specifically from
/// the OpenSubtitles provider; any newly-appeared, non-aligned external
/// subtitle on a movie is treated as eligible.
/// </summary>
public sealed class SubtitleDownloadWatcher : IHostedService
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(5);

    private readonly ILibraryManager _libraryManager;
    private readonly ISubtitleAlignmentService _alignmentService;
    private readonly ILogger<SubtitleDownloadWatcher> _logger;
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _knownSubtitlePaths = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _lastProcessedUtc = new();
    private readonly Channel<AlignmentRequest> _channel = Channel.CreateBounded<AlignmentRequest>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubtitleDownloadWatcher"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager, used to observe item updates.</param>
    /// <param name="alignmentService">Service used to perform the actual alignment.</param>
    /// <param name="logger">Logger.</param>
    public SubtitleDownloadWatcher(
        ILibraryManager libraryManager,
        ISubtitleAlignmentService alignmentService,
        ILogger<SubtitleDownloadWatcher> logger)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _alignmentService = alignmentService ?? throw new ArgumentNullException(nameof(alignmentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated += OnItemUpdated;

        _workerCts = new CancellationTokenSource();
        _workerTask = Task.Run(() => ProcessQueueAsync(_workerCts.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated -= OnItemUpdated;

        _channel.Writer.TryComplete();
        _workerCts?.Cancel();

        if (_workerTask is not null)
        {
            try
            {
                await _workerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        if (e.Item is not Movie movie)
        {
            return;
        }

        if (_lastProcessedUtc.TryGetValue(movie.Id, out var lastProcessed)
            && DateTime.UtcNow - lastProcessed < DebounceWindow)
        {
            return;
        }

        _lastProcessedUtc[movie.Id] = DateTime.UtcNow;

        var videoPath = movie.Path;
        if (string.IsNullOrEmpty(videoPath))
        {
            return;
        }

        var currentSubtitlePaths = movie.GetMediaStreams()
            .Where(stream => stream.Type == MediaBrowser.Model.Entities.MediaStreamType.Subtitle
                && stream.IsExternal
                && !string.IsNullOrEmpty(stream.Path))
            .Select(stream => stream.Path)
            .ToList();

        var previousSubtitlePaths = _knownSubtitlePaths.GetOrAdd(movie.Id, _ => new HashSet<string>());
        var newPaths = SubtitleChangeDetector.DiffNewSubtitlePaths(previousSubtitlePaths, currentSubtitlePaths);

        _knownSubtitlePaths[movie.Id] = new HashSet<string>(currentSubtitlePaths, StringComparer.OrdinalIgnoreCase);

        foreach (var subtitlePath in newPaths)
        {
            if (!SubtitleChangeDetector.IsCandidateSubtitle(subtitlePath))
            {
                continue;
            }

            var request = new AlignmentRequest(movie.Id, videoPath, subtitlePath);
            if (!_channel.Writer.TryWrite(request))
            {
                _logger.LogWarning("Alignment queue full; dropped request for {SubtitlePath}", subtitlePath);
            }
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await _alignmentService.AlignAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected error aligning subtitle {SubtitlePath}", request.SubtitlePath);
            }
        }
    }
}
