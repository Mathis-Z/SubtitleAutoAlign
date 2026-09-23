using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleAutoAlign.Models;
using Jellyfin.Plugin.SubtitleAutoAlign.Services;
using Xunit;

namespace Jellyfin.Plugin.SubtitleAutoAlign.Tests;

public class BulkAlignerTests
{
    [Fact]
    public async Task RunAsync_CountsAlignedSkippedAndFailed()
    {
        var service = new ScriptedAlignmentService(Aligned, Skipped, Failed, Aligned);

        var summary = await BulkAligner.RunAsync(Requests(4), service, new RecordingProgress(), CancellationToken.None);

        Assert.Equal(new BulkAlignmentSummary(Aligned: 2, Skipped: 1, Failed: 1), summary);
    }

    [Fact]
    public async Task RunAsync_ContinuesAfterFailure()
    {
        var service = new ScriptedAlignmentService(Failed, Aligned);

        await BulkAligner.RunAsync(Requests(2), service, new RecordingProgress(), CancellationToken.None);

        Assert.Equal(2, service.Calls.Count);
    }

    [Fact]
    public async Task RunAsync_ReportsProgressUpTo100()
    {
        var progress = new RecordingProgress();

        await BulkAligner.RunAsync(Requests(4), new ScriptedAlignmentService(Aligned, Aligned, Aligned, Aligned), progress, CancellationToken.None);

        Assert.Equal(new[] { 25.0, 50.0, 75.0, 100.0, 100.0 }, progress.Values);
    }

    [Fact]
    public async Task RunAsync_NoRequests_ReportsDone()
    {
        var progress = new RecordingProgress();

        var summary = await BulkAligner.RunAsync([], new ScriptedAlignmentService(), progress, CancellationToken.None);

        Assert.Equal(new BulkAlignmentSummary(0, 0, 0), summary);
        Assert.Equal(new[] { 100.0 }, progress.Values);
    }

    [Fact]
    public async Task RunAsync_Cancelled_StopsBeforeNextSubtitle()
    {
        using var cts = new CancellationTokenSource();
        var service = new ScriptedAlignmentService(Aligned, Aligned, Aligned) { OnCall = cts.Cancel };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => BulkAligner.RunAsync(Requests(3), service, new RecordingProgress(), cts.Token));

        Assert.Single(service.Calls);
    }

    private static AlignmentResult Aligned => new(Success: true, OutputPath: "out.srt", Skipped: false, ErrorMessage: null);

    private static AlignmentResult Skipped => new(Success: false, OutputPath: "out.srt", Skipped: true, ErrorMessage: null);

    private static AlignmentResult Failed => new(Success: false, OutputPath: "out.srt", Skipped: false, ErrorMessage: "boom");

    private static List<AlignmentRequest> Requests(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new AlignmentRequest(Guid.NewGuid(), $"movie{i}.mkv", $"movie{i}.en.srt"))
            .ToList();

    private sealed class ScriptedAlignmentService : ISubtitleAlignmentService
    {
        private readonly Queue<AlignmentResult> _results;

        public ScriptedAlignmentService(params AlignmentResult[] results)
        {
            _results = new Queue<AlignmentResult>(results);
        }

        public List<AlignmentRequest> Calls { get; } = [];

        public Action? OnCall { get; init; }

        public Task<AlignmentResult> AlignAsync(AlignmentRequest request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            OnCall?.Invoke();
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class RecordingProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }
}
