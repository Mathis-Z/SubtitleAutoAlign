using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// Runs ffsubsync to produce an aligned copy of a subtitle file.
/// </summary>
public interface ISubtitleAlignmentService
{
    /// <summary>
    /// Aligns the subtitle described by <paramref name="request"/> against its
    /// reference video, writing the result alongside the original.
    /// </summary>
    /// <param name="request">The alignment request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The outcome of the alignment attempt. Never throws on ffsubsync
    /// failure; callers can rely on the returned result instead.</returns>
    Task<AlignmentResult> AlignAsync(AlignmentRequest request, CancellationToken cancellationToken);
}
