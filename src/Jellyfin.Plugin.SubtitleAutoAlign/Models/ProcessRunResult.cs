namespace Jellyfin.Plugin.SubtitleAutoAlign.Models;

/// <summary>
/// Result of running an external process to completion (or timeout).
/// </summary>
/// <param name="ExitCode">Process exit code; meaningless if <see cref="TimedOut"/> is true.</param>
/// <param name="StandardOutput">Captured standard output.</param>
/// <param name="StandardError">Captured standard error.</param>
/// <param name="TimedOut">True if the process was killed because it exceeded its timeout.</param>
public sealed record ProcessRunResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);
