using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Services;

/// <summary>
/// Abstraction over external process execution. Exists so alignment logic can
/// be unit tested without shelling out to a real ffsubsync binary.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs the given executable with the given arguments to completion, or
    /// until <paramref name="timeout"/> elapses, whichever comes first.
    /// </summary>
    /// <param name="fileName">Executable name or path.</param>
    /// <param name="arguments">Ordered argument list.</param>
    /// <param name="timeout">Maximum time to allow the process to run.</param>
    /// <param name="cancellationToken">Token to cancel the run early.</param>
    /// <returns>The captured result of the run.</returns>
    Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        System.TimeSpan timeout,
        CancellationToken cancellationToken);
}
