using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// Outcome counts of a bulk alignment run.
/// </summary>
/// <param name="Aligned">Subtitles aligned successfully.</param>
/// <param name="Skipped">Subtitles skipped, e.g. because an aligned copy already exists.</param>
/// <param name="Failed">Subtitles ffsubsync could not align.</param>
public sealed record BulkAlignmentSummary(int Aligned, int Skipped, int Failed);

/// <summary>
/// Aligns a list of subtitles one after another, reporting progress.
/// </summary>
public static class BulkAligner
{
    /// <summary>
    /// Aligns each request in order. A failed alignment is counted and does not stop the run.
    /// </summary>
    /// <param name="requests">The subtitles to align.</param>
    /// <param name="alignmentService">Service that performs each alignment.</param>
    /// <param name="progress">Receives progress from 0 to 100.</param>
    /// <param name="cancellationToken">Stops the run before the next subtitle.</param>
    /// <returns>How many subtitles were aligned, skipped and failed.</returns>
    public static async Task<BulkAlignmentSummary> RunAsync(
        IReadOnlyList<AlignmentRequest> requests,
        ISubtitleAlignmentService alignmentService,
        IProgress<double> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(alignmentService);
        ArgumentNullException.ThrowIfNull(progress);

        int aligned = 0, skipped = 0, failed = 0;

        for (var i = 0; i < requests.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await alignmentService.AlignAsync(requests[i], cancellationToken).ConfigureAwait(false);
            if (result.Skipped)
            {
                skipped++;
            }
            else if (result.Success)
            {
                aligned++;
            }
            else
            {
                failed++;
            }

            progress.Report(100.0 * (i + 1) / requests.Count);
        }

        progress.Report(100);
        return new BulkAlignmentSummary(aligned, skipped, failed);
    }
}
