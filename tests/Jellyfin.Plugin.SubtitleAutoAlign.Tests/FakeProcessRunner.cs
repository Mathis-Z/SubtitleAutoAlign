using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

/// <summary>
/// Test double for <see cref="IProcessRunner"/> that returns a canned result
/// instead of shelling out, and records the arguments it was called with.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    public ProcessRunResult ResultToReturn { get; set; } = new(0, string.Empty, string.Empty, false);

    public string? LastFileName { get; private set; }

    public IReadOnlyList<string>? LastArguments { get; private set; }

    public int CallCount { get; private set; }

    public Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        System.TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastFileName = fileName;
        LastArguments = arguments;
        return Task.FromResult(ResultToReturn);
    }
}
