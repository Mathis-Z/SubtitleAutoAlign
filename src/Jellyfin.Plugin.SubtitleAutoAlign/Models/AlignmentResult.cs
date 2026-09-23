namespace Jellyfin.Plugin.SubtitleAutoAlign.Models;

/// <summary>
/// Outcome of an alignment attempt.
/// </summary>
/// <param name="Success">True if the aligned subtitle was produced successfully.</param>
/// <param name="OutputPath">Path the aligned subtitle was (or would have been) written to.</param>
/// <param name="Skipped">True if the request was skipped without invoking ffsubsync
/// (e.g. disabled, or output already exists).</param>
/// <param name="ErrorMessage">Human-readable failure reason, if any.</param>
public sealed record AlignmentResult(
    bool Success,
    string OutputPath,
    bool Skipped,
    string? ErrorMessage);
